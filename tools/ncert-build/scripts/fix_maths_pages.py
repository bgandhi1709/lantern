#!/usr/bin/env python3
"""Re-read pages the local text extraction flagged as unreliable (`maths_dense`, `garbled`)
against the actual PDF page image, and patch `text/<book>/<chapter>.json` with a corrected
transcription - the "optional vision pass" the README's Known Limits section describes.

    tools/ncert-build/.venv/bin/python scripts/fix_maths_pages.py [--budget-usd 8] [--dry-run]

One Sonnet 5.5 vision call per flagged page (thinking off, no retries - see ncert_build/claude.py
and its README section). Stops before spending past --budget-usd. Idempotent: a page already
marked "vision_checked" in text/*.json is skipped on a rerun, so an interrupted run just resumes.
After patching, re-runs `segment` locally (free) for every touched book, and reports which
chapters actually changed - those are the ones whose already-refined concept cards/Q&A were built
from the broken text and are candidates for re-refining, a separate cost this script does not
spend on its own.
"""
from __future__ import annotations

import argparse
import io
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import pypdfium2 as pdfium

from ncert_build import catalog, pipeline
from ncert_build.claude import Claude
from ncert_build.config import Settings

MODEL = "claude-sonnet-5-5"
FLAGS_TO_FIX = {"maths_dense", "garbled"}

SYSTEM = (
    "You correct OCR-style text extraction errors in NCERT textbook pages for an Indian "
    "education app. You are given the page image and the plain-text extraction our pipeline "
    "produced from the PDF's text layer. The extraction can lose math notation (superscripts, "
    "fractions, roots split across lines) and can misread punctuation as the wrong character "
    "due to font encoding (a '?' becoming '‘', a letter becoming an en-dash, etc). "
    "Produce a corrected plain-text transcription: use standard notation for math (5³ for "
    "exponents, 3/4 for fractions, √2 for roots), fix corrupted punctuation and letters, but "
    "otherwise keep the same wording, line breaks and structure as the extraction where it is "
    "already correct - do not paraphrase or improve the prose. If the extraction is already "
    "correct, reply with it unchanged. Reply with only the corrected text, nothing else."
)


def render_page(pdf_path: Path, page_number: int) -> bytes:
    doc = pdfium.PdfDocument(pdf_path)
    try:
        bitmap = doc[page_number - 1].render(scale=2.0)
        buf = io.BytesIO()
        bitmap.to_pil().save(buf, format="PNG")
        return buf.getvalue()
    finally:
        doc.close()


def normalized(text: str) -> str:
    return " ".join(text.split())


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--budget-usd", type=float, default=8.0)
    parser.add_argument("--dry-run", action="store_true", help="list flagged pages, call nothing")
    args = parser.parse_args()

    settings = Settings.from_env()
    layout = pipeline.Layout(settings.data_dir)
    books = catalog.load(layout.catalog)
    client = None if args.dry_run else Claude()

    spent = 0.0
    checked, changed_pages, failed = 0, 0, 0
    changed_books: dict[str, set[str]] = {}
    budget_hit = False

    for book in books:
        if budget_hit:
            break
        for chapter_id in book.chapter_ids():
            text_path = layout.text(book.book_id, chapter_id)
            pdf_path = layout.pdf(book.book_id, chapter_id)
            if not text_path.exists() or not pdf_path.exists():
                continue
            doc = json.loads(text_path.read_text(encoding="utf-8"))
            pending = [p for p in doc["pages"] if set(p.get("flags", [])) & FLAGS_TO_FIX and not p.get("vision_checked")]
            if not pending:
                continue
            chapter_touched = False
            for page in pending:
                if spent >= args.budget_usd:
                    print(f"budget reached (${spent:.4f} spent), stopping")
                    budget_hit = True
                    break
                if args.dry_run:
                    print(f"{chapter_id} p{page['number']}: would check ({', '.join(page['flags'])})")
                    continue
                try:
                    image = render_page(pdf_path, page["number"])
                    prompt = f"Current extraction of this page:\n\n{page['text']}"
                    text, usage = client.complete(MODEL, prompt, system=SYSTEM, max_tokens=1500, images=[image])
                except Exception as error:  # noqa: BLE001 - one attempt, report and move on
                    print(f"{chapter_id} p{page['number']}: failed, skipping (no retry): {error}")
                    failed += 1
                    continue
                cost = usage.cost_usd(MODEL)
                spent += cost
                checked += 1
                page["vision_checked"] = True
                if not text or normalized(text) == normalized(page["text"]):
                    print(f"{chapter_id} p{page['number']}: unchanged (${cost:.5f}, total ${spent:.4f})")
                    continue
                page["text"] = text
                changed_pages += 1
                chapter_touched = True
                print(f"{chapter_id} p{page['number']}: corrected (${cost:.5f}, total ${spent:.4f})")
            if not args.dry_run:
                text_path.write_text(json.dumps(doc, indent=2, ensure_ascii=False), encoding="utf-8")
            if chapter_touched:
                changed_books.setdefault(book.book_id, set()).add(chapter_id)
            if budget_hit:
                break

    print(f"\n{checked} pages checked, {changed_pages} corrected, {failed} failed, ${spent:.4f} spent")

    if changed_books and not args.dry_run:
        touched = [b for b in books if b.book_id in changed_books]
        print(f"\nre-running segment for {len(touched)} touched book(s)...")
        report = pipeline.segment_chapters(layout, touched, force=True, verbose=False)
        print(report.line())
        print("\nchapters with a real text change (candidates for re-refining, not done here):")
        for book_id, chapter_ids in sorted(changed_books.items()):
            for chapter_id in sorted(chapter_ids):
                print(f"  {chapter_id}")


if __name__ == "__main__":
    main()
