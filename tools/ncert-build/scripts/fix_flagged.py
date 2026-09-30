#!/usr/bin/env python3
"""Fix the handful of refine-v1 rule violations `ncert-build check` finds, one minimal API
call per violation (Haiku 4.5, no thinking, no retries) - not per chapter, so a chapter with
one bad field costs one tiny call, not a full re-refine.

    tools/ncert-build/.venv/bin/python scripts/fix_flagged.py [--budget-usd 5] [--dry-run]

Stops before spending past --budget-usd (checked after every call, tracked from real
usage - see ncert_build/claude.py). A call that errors or an error message this script
doesn't recognise is reported and skipped, never retried.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from ncert_build import catalog, pipeline
from ncert_build.claude import Claude
from ncert_build.config import Settings

MODEL = "claude-sonnet-5-5"

NAME_RE = re.compile(r"^concept (\d+): name must be 1–(\d+) characters$")
WORD_RE = re.compile(r"^concept (\d+): (summary|how_taught) over (\d+) words$")
QA_WORD_RE = re.compile(r"^concept (\d+) qa (\d+): (try) over (\d+) words$")
MARKDOWN_RE = re.compile(r"^concept (\d+) qa (\d+): no markdown in answers$")


def _fix_prompt(current: str, limit: int, unit: str, extra: str = "") -> str:
    return (
        f"Shorten this text to at most {limit} {unit}. Keep the meaning.{extra} "
        f"Reply with only the shortened text, nothing else - no surrounding quotation marks, "
        f"no explanation.\n\n{current}"
    )


def plan_fix(error: str, concepts: list[dict]) -> tuple[str, str] | None:
    """Returns (json_path_description, prompt) for one error, or None if unrecognised."""
    if m := NAME_RE.match(error):
        c = concepts[int(m.group(1)) - 1]
        return f"concepts[{int(m.group(1))-1}].name", _fix_prompt(c["name"], int(m.group(2)), "characters")
    if m := WORD_RE.match(error):
        idx, field, limit = int(m.group(1)) - 1, m.group(2), int(m.group(3))
        return f"concepts[{idx}].{field}", _fix_prompt(concepts[idx][field], limit, "words")
    if m := QA_WORD_RE.match(error):
        ci, qi, field, limit = int(m.group(1)) - 1, int(m.group(2)) - 1, m.group(3), int(m.group(4))
        return f"concepts[{ci}].qa[{qi}].{field}", _fix_prompt(concepts[ci]["qa"][qi][field], limit, "words")
    if m := MARKDOWN_RE.match(error):
        ci, qi = int(m.group(1)) - 1, int(m.group(2)) - 1
        current = concepts[ci]["qa"][qi]["a"]
        extra = " Remove all markdown characters (*, _, #, `) - plain text only. Stay under 60 words."
        return f"concepts[{ci}].qa[{qi}].a", _fix_prompt(current, 60, "words", extra)
    return None


def apply_fix(concepts: list[dict], path: str, new_value: str) -> None:
    target: dict | list = concepts
    parts = re.findall(r"(\w+)|\[(\d+)\]", path)
    *steps, (last_key, last_idx) = parts
    for key, idx in steps:
        target = target[key] if key else target[int(idx)]
    if last_key:
        target[last_key] = new_value
    else:
        target[int(last_idx)] = new_value


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--budget-usd", type=float, default=5.0)
    parser.add_argument("--dry-run", action="store_true", help="show planned fixes, call nothing")
    args = parser.parse_args()

    settings = Settings.from_env()
    layout = pipeline.Layout(settings.data_dir)
    books = {b.book_id: b for b in catalog.load(layout.catalog)}
    client = None if args.dry_run else Claude()

    spent = 0.0
    fixed, unfixed, unrecognised = 0, 0, 0

    for book in books.values():
        for chapter_id in book.chapter_ids():
            path = layout.refined(book.book_id, chapter_id)
            if not path.exists():
                continue
            errors = pipeline.check_refined(layout, book, chapter_id)
            if not errors:
                continue
            refined = json.loads(path.read_text(encoding="utf-8"))
            concepts = refined["concepts"]
            changed = False
            for error in errors:
                plan = plan_fix(error, concepts)
                if plan is None:
                    print(f"{chapter_id}: unrecognised error, skipping: {error}")
                    unrecognised += 1
                    continue
                target_path, prompt = plan
                if args.dry_run:
                    print(f"{chapter_id}: would fix {target_path}")
                    continue
                if spent >= args.budget_usd:
                    print(f"{chapter_id}: budget reached (${spent:.4f} spent), stopping")
                    print(f"unfixed from here: {chapter_id} {target_path} and any after it")
                    _report(fixed, unfixed, unrecognised, spent)
                    return
                try:
                    text, usage = client.complete(MODEL, prompt, max_tokens=120)
                except RuntimeError as error_detail:
                    print(f"{chapter_id}: API call failed for {target_path}, skipping (no retry): {error_detail}")
                    unfixed += 1
                    continue
                cost = usage.cost_usd(MODEL)
                spent += cost
                if not text:
                    print(f"{chapter_id}: empty reply for {target_path}, leaving original value (no retry, cost ${cost:.6f})")
                    unfixed += 1
                    continue
                apply_fix(refined, target_path, text)
                changed = True
                fixed += 1
                print(f"{chapter_id}: fixed {target_path} (${cost:.6f}, total ${spent:.4f})")
            if changed and not args.dry_run:
                path.write_text(json.dumps(refined, indent=2, ensure_ascii=False), encoding="utf-8")
                remaining = pipeline.check_refined(layout, book, chapter_id)
                print(f"{chapter_id}: {'ok' if not remaining else f'{len(remaining)} problem(s) remain'}")

    _report(fixed, unfixed, unrecognised, spent)


def _report(fixed: int, unfixed: int, unrecognised: int, spent: float) -> None:
    print(f"\n{fixed} fixed, {unfixed} failed, {unrecognised} unrecognised, ${spent:.4f} spent")


if __name__ == "__main__":
    main()
