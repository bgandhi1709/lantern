"""Command line: ``ncert-build <stage> [filters]``. Run ``ncert-build --help`` for the stages."""

from __future__ import annotations

import argparse
import logging
import sys

from . import catalog, draft, pipeline
from .config import CORE_SUBJECTS, Settings
from .ollama import Ollama


def parse_grades(text: str | None) -> set[int] | None:
    """'1-3,10' -> {1, 2, 3, 10}"""
    if not text:
        return None
    grades: set[int] = set()
    for part in text.split(","):
        low, _, high = part.strip().partition("-")
        grades.update(range(int(low), int(high or low) + 1))
    return grades


def _selected(args: argparse.Namespace, layout: pipeline.Layout) -> list[catalog.Book]:
    subjects = None if args.all_subjects else set(args.subjects or CORE_SUBJECTS)
    books = catalog.select(
        catalog.load(layout.catalog),
        grades=parse_grades(args.grades),
        subjects=subjects,
        mediums={args.medium},
        book_ids=set(args.books) if args.books else None,
    )
    if not books:
        raise SystemExit("No books match those filters. `ncert-build list` shows what's available.")
    return books


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="ncert-build", description=__doc__)
    stages = parser.add_subparsers(dest="stage", required=True)

    stages.add_parser("catalog", help="fetch NCERT's book list into catalog.json")
    for name, text in [
        ("list", "show the books the filters select"),
        ("download", "download chapter PDFs (and each book's front matter)"),
        ("extract", "extract page text and quality flags"),
        ("segment", "clean text and split chapters into sections"),
        ("draft", "draft concepts and kid questions with the local model"),
        ("run", "download, extract, segment and draft in one go"),
        ("status", "count what each stage has produced"),
    ]:
        stage = stages.add_parser(name, help=text)
        stage.add_argument("--grades", help="e.g. 1-3,10 (default: 1-10)", default="1-10")
        stage.add_argument("--subjects", nargs="+", help=f"default: {', '.join(CORE_SUBJECTS)}")
        stage.add_argument("--all-subjects", action="store_true", help="include arts, PE and vocational books")
        stage.add_argument("--medium", default="English", help="book language (default: English)")
        stage.add_argument("--books", nargs="+", help="book ids, e.g. aejm1 jemh1")
        stage.add_argument("--force", action="store_true", help="redo chapters that already have output")
        stage.add_argument("--model", help="Ollama model for drafts (default: LANTERN_DRAFT_MODEL or qwen3:8b)")
        stage.add_argument("-v", "--verbose", action="store_true")

    args = parser.parse_args(argv)
    logging.getLogger("pypdf").setLevel(logging.ERROR)  # font-encoding warnings are noise here

    settings = Settings.from_env()
    settings.ensure_outside_repo()
    layout = pipeline.Layout(settings.data_dir)

    if args.stage == "catalog":
        books = pipeline.refresh_catalog(layout)
        print(f"{len(books)} books written to {layout.catalog}")
        return 0

    books = _selected(args, layout)
    verbose = args.verbose

    if args.stage == "list":
        for book in books:
            print(f"{book.book_id:8} Class {book.grade:<2} {book.subject:24} {book.title} ({len(book.chapter_ids())} chapters)")
        print(f"{len(books)} books, {sum(len(b.chapter_ids()) for b in books)} chapters")
        return 0

    if args.stage == "status":
        print(f"{'book':60} chapters  pdf  text  sections  drafts")
        for name, total, pdfs, texts, chapters, drafts in pipeline.status(layout, books):
            print(f"{name[:60]:60} {total:8} {pdfs:4} {texts:5} {chapters:9} {drafts:7}")
        return 0

    client = None
    if args.stage in ("draft", "run"):
        # Checked first so a run fails fast instead of after an hour of downloads.
        model = args.model or settings.draft_model
        client = Ollama.connect(settings.ollama_host)
        client.require(model)

    reports = []
    if args.stage in ("download", "run"):
        reports.append(pipeline.download(layout, settings, books, verbose))
    if args.stage in ("extract", "run"):
        reports.append(pipeline.extract_text(layout, books, args.force, verbose))
    if args.stage in ("segment", "run"):
        reports.append(pipeline.segment_chapters(layout, books, args.force, verbose))
    if client is not None:
        print(f"Drafting with {model} at {client.host}")

        def chat(system: str, user: str) -> dict:
            return client.chat_json(model, system, user, draft.SCHEMA, draft.NUM_CTX)

        reports.append(pipeline.draft_chapters(layout, books, chat, model, args.force, verbose))

    for report in reports:
        print(report.line())
    return 1 if any(r.failed for r in reports) else 0


if __name__ == "__main__":
    sys.exit(main())
