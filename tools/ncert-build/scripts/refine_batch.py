#!/usr/bin/env python3
"""Refine SSC Chapters through the Batch API (#97, D55): half price, no retries, a budget cap.

    source ~/.lantern_api.env
    .venv/bin/python scripts/refine_batch.py submit --chapters ssc3-maths-04 … --efforts low medium high
    .venv/bin/python scripts/refine_batch.py collect <batch id> --out "refined-gate/{model}-{effort}"

`submit` refuses to send a batch whose worst case (every request writing a quarter of max_tokens)
passes --budget-usd. `collect` waits for the batch, writes each Chapter, runs `check` on it and
prints the real cost per Chapter from `usage`. A failed or truncated request is listed, never
retried, and writes nothing. `--out refined` writes the corpus itself.
"""

from __future__ import annotations

import argparse
import json
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from ncert_build import batch_refine, pipeline, refine_check
from ncert_build.catalog import book_id_of
from ncert_build.claude import Claude, Usage
from ncert_build.config import Settings

MODEL = "claude-sonnet-5-5"
WORST_CASE_OUTPUT = batch_refine.MAX_TOKENS // 4


def _layout() -> pipeline.Layout:
    settings = Settings.from_env("ssc")
    settings.ensure_outside_repo()
    return pipeline.Layout(settings.data_dir)


def _lessons(layout: pipeline.Layout, chapter_id: str) -> list[dict]:
    book_id = book_id_of(chapter_id)
    return batch_refine.unit_lessons(layout.toc(book_id), chapter_id) if book_id.endswith("-english") else []


def submit(args: argparse.Namespace) -> int:
    layout = _layout()
    requests, worst = [], Usage(0, 0)
    for chapter_id in args.chapters:
        bundle = layout.bundle(book_id_of(chapter_id), chapter_id).read_text(encoding="utf-8")
        text = batch_refine.chapter_input(bundle, _lessons(layout, chapter_id))
        for effort in args.efforts:
            requests.append(batch_refine.request(f"{chapter_id}__{effort}", args.model, effort, text))
            worst = Usage(
                worst.input_tokens + (len(batch_refine.SYSTEM) + len(text)) // 3,
                worst.output_tokens + WORST_CASE_OUTPUT,
            )
    ceiling = worst.cost_usd(args.model, batch=True)
    print(f"{len(requests)} requests, worst case ${ceiling:.2f} (₹{ceiling * 96:.0f} at ₹96/$), budget ${args.budget_usd:.2f}")
    if ceiling > args.budget_usd:
        print("Over budget: nothing sent.")
        return 1
    if args.dry_run:
        return 0
    batch = Claude().create_batch(requests)
    log = layout.root / "logs" / "batches.jsonl"
    log.parent.mkdir(parents=True, exist_ok=True)
    with log.open("a", encoding="utf-8") as handle:
        entry = {"id": batch["id"], "at": datetime.now(timezone.utc).isoformat(), "model": args.model}
        handle.write(json.dumps({**entry, "efforts": args.efforts, "chapters": args.chapters}) + "\n")
    print(f"batch {batch['id']} submitted")
    return 0


def _write(layout: pipeline.Layout, out: str, model: str, chapter_id: str, effort: str, message: dict) -> tuple[Path, dict]:
    text = "".join(block["text"] for block in message["content"] if block["type"] == "text")
    refined = batch_refine.assemble(json.loads(text), chapter_id, model, _lessons(layout, chapter_id))
    short = model.removeprefix("claude-")
    target = layout.root / out.format(model=short, effort=effort) / book_id_of(chapter_id) / f"{chapter_id}.json"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(refined, indent=2, ensure_ascii=False), encoding="utf-8")
    return target, refined


def collect(args: argparse.Namespace) -> int:
    layout, client = _layout(), Claude()
    batch = client.get_batch(args.batch_id)
    while batch["processing_status"] != "ended":
        print(f"{batch['processing_status']}: {batch['request_counts']}", flush=True)
        time.sleep(30)
        batch = client.get_batch(args.batch_id)

    totals: dict[str, list[float]] = {}
    failed = 0
    for result in sorted(client.batch_results(batch), key=lambda r: r["custom_id"]):
        chapter_id, effort = result["custom_id"].split("__")
        outcome = result["result"]
        if outcome["type"] != "succeeded":
            failed += 1
            print(f"{chapter_id} {effort}: {outcome['type']} {outcome.get('error', '')}")
            continue
        message = outcome["message"]
        usage = Usage.from_api(message["usage"])
        cost = usage.cost_usd(message["model"], batch=True)
        totals.setdefault(effort, [0.0, 0])
        totals[effort][0] += cost
        totals[effort][1] += 1
        if message["stop_reason"] != "end_turn":
            failed += 1
            print(f"{chapter_id} {effort}: stopped on {message['stop_reason']}, ${cost:.3f}, nothing written")
            continue
        _, refined = _write(layout, args.out, message["model"], chapter_id, effort, message)
        pages = pipeline.chapter_page_count(layout, book_id_of(chapter_id), chapter_id)
        errors = refine_check.check(refined, chapter_id, pages)
        qa = [pair for concept in refined["concepts"] for pair in concept["qa"]]
        print(
            f"{chapter_id} {effort}: in {usage.input_tokens} out {usage.output_tokens} ${cost:.3f}, "
            f"{refine_check.summary(refined)}, try {sum('try' in pair for pair in qa)}, {len(errors)} check errors"
        )
        for error in errors[:3]:
            print(f"   - {error}")
    for effort, (cost, count) in totals.items():
        print(f"{effort}: {count} Chapters ${cost:.3f} (₹{cost * 96:.1f}), ${cost / count:.4f} per Chapter")
    return 1 if failed else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest="command", required=True)
    sending = commands.add_parser("submit")
    sending.add_argument("--chapters", nargs="+", required=True)
    sending.add_argument("--efforts", nargs="+", default=["high"], choices=["low", "medium", "high"])
    sending.add_argument("--model", default=MODEL)
    sending.add_argument("--budget-usd", type=float, default=3.0)
    sending.add_argument("--dry-run", action="store_true")
    collecting = commands.add_parser("collect")
    collecting.add_argument("batch_id")
    collecting.add_argument("--out", default="refined-gate/{model}-{effort}", help='"refined" writes the corpus')
    args = parser.parse_args()
    return submit(args) if args.command == "submit" else collect(args)


if __name__ == "__main__":
    sys.exit(main())
