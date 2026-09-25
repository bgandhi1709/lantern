"""Command line: ``ncert-build <stage> [filters]``. Run ``ncert-build --help`` for the stages."""

from __future__ import annotations

import argparse
import json
import logging
import os
import sys

from . import catalog, draft, gpu, pipeline, refine_check, render, review, upload
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


def _check(layout: pipeline.Layout, chapter_ids: list[str]) -> int:
    """Prints one line per chapter; exits 1 if any chapter breaks the refine-v1 contract."""
    books = {b.book_id: b for b in catalog.load(layout.catalog)}
    failed = 0
    for chapter_id in chapter_ids:
        book = books.get(chapter_id[:-2])
        if book is None:
            print(f"{chapter_id}: unknown book")
            failed += 1
            continue
        errors = pipeline.check_refined(layout, book, chapter_id)
        if errors:
            failed += 1
            print(f"{chapter_id}: {len(errors)} problem(s)")
            for error in errors:
                print(f"  - {error}")
        else:
            refined = json.loads(layout.refined(book.book_id, chapter_id).read_text(encoding="utf-8"))
            print(f"{chapter_id}: ok, {refine_check.summary(refined)}")
    return 1 if failed else 0


def shutdown_windows(minutes: int = 5) -> None:
    """Asks Windows (from WSL) to shut down; `shutdown /a` in a Windows terminal cancels it."""
    import subprocess

    command = ["/mnt/c/Windows/System32/shutdown.exe", "/s", "/t", str(minutes * 60), "/c", "Lantern build finished"]
    try:
        subprocess.run(command, check=False, timeout=30)
        print(f"Windows shuts down in {minutes} minutes (cancel: shutdown /a)", flush=True)
    except OSError as error:
        print(f"Couldn't ask Windows to shut down: {error}", flush=True)


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        return _run(args)
    finally:
        if getattr(args, "shutdown", False):
            shutdown_windows()


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="ncert-build", description=__doc__)
    stages = parser.add_subparsers(dest="stage", required=True)

    stages.add_parser("catalog", help="fetch NCERT's book list into catalog.json")
    uploading = stages.add_parser("upload", help="copy the build output to the private `ncert` blob container")
    uploading.add_argument(
        "--account",
        default=os.environ.get("LANTERN_STORAGE_ACCOUNT"),
        help="storage account name (default: LANTERN_STORAGE_ACCOUNT)",
    )
    for name, text in [
        ("list", "show the books the filters select"),
        ("download", "download chapter PDFs (and each book's front matter)"),
        ("extract", "extract page text and quality flags"),
        ("segment", "clean text and split chapters into sections"),
        ("draft", "draft concepts and kid questions with the local model"),
        ("pictures", "describe picture pages (low text) with the local vision model"),
        ("run", "download, extract, segment, draft and describe pictures in one go"),
        ("bundle", "write the compact per-chapter input for Claude refinement"),
        ("check", "validate refined chapters (pass chapter ids, or use the filters)"),
        ("review", "write the picture review sheet and the agreement report (no model)"),
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
        stage.add_argument(
            "--vision-model", help="Ollama model for picture pages (default: LANTERN_VISION_MODEL or qwen3-vl:8b-instruct)"
        )
        stage.add_argument("--chapters", nargs="+", help="only these chapter ids, e.g. aejm113 gegp108")
        stage.add_argument(
            "--shutdown", action="store_true", help="shut Windows down 5 minutes after this finishes, even on failure"
        )
        stage.add_argument("-v", "--verbose", action="store_true")
        if name == "check":
            stage.add_argument("chapters", nargs="*", help="chapter ids, e.g. aejm104")

    return parser


def _run(args: argparse.Namespace) -> int:
    logging.getLogger("pypdf").setLevel(logging.ERROR)  # font-encoding warnings are noise here

    settings = Settings.from_env()
    settings.ensure_outside_repo()
    layout = pipeline.Layout(settings.data_dir)

    if args.stage == "catalog":
        books = pipeline.refresh_catalog(layout)
        print(f"{len(books)} books written to {layout.catalog}")
        return 0

    if args.stage == "upload":
        if not args.account:
            raise SystemExit("Pass --account or set LANTERN_STORAGE_ACCOUNT (the deployment prints it).")
        upload.run(settings.data_dir, args.account)
        return 0

    if args.stage == "check" and args.chapters:
        return _check(layout, args.chapters)

    books = _selected(args, layout)
    verbose = args.verbose
    if args.chapters:
        pipeline.only_chapters = set(args.chapters)

    if args.stage == "review":
        label = "class-" + args.grades.replace(",", "_") if args.grades != "1-10" else "all"
        print(review.write(layout, books, label))
        return 0

    if args.stage == "check":
        return _check(layout, [c for b in books for c in b.chapter_ids() if layout.refined(b.book_id, c).exists()])

    if args.stage == "bundle":
        report = pipeline.bundle_chapters(layout, books, args.force, verbose)
        print(report.line())
        return 1 if report.failed else 0

    if args.stage == "list":
        for book in books:
            print(f"{book.book_id:8} Class {book.grade:<2} {book.subject:24} {book.title} ({len(book.chapter_ids())} chapters)")
        print(f"{len(books)} books, {sum(len(b.chapter_ids()) for b in books)} chapters")
        return 0

    if args.stage == "status":
        rows = pipeline.status(layout, books)
        print(f"{'book':60} chapters  pdf  text  sections  drafts  refined")
        for name, total, pdfs, texts, chapters, drafts, refined in rows:
            print(f"{name[:60]:60} {total:8} {pdfs:4} {texts:5} {chapters:9} {drafts:7} {refined:8}")
        totals = [sum(column) for column in list(zip(*rows))[1:]]
        print(f"{'TOTAL':60} {totals[0]:8} {totals[1]:4} {totals[2]:5} {totals[3]:9} {totals[4]:7} {totals[5]:8}")
        ready, drafted = totals[3], totals[4]
        print(f"\nDrafted {drafted} of {ready} available chapters ({100 * drafted // max(ready, 1)}%), {ready - drafted} to go.")
        return 0

    client = None
    model = args.model or settings.draft_model
    vision_model = args.vision_model or settings.vision_model
    if args.stage in ("draft", "pictures", "run"):
        # Checked first so a run fails fast instead of after an hour of downloads.
        client = Ollama.connect(settings.ollama_host)
        if args.stage in ("draft", "run"):
            client.require(model)
        if args.stage in ("pictures", "run"):
            client.require(vision_model)

    reports = []
    if args.stage in ("download", "run"):
        reports.append(pipeline.download(layout, settings, books, verbose))
    if args.stage in ("extract", "run"):
        reports.append(pipeline.extract_text(layout, books, args.force, verbose))
    if args.stage in ("segment", "run"):
        reports.append(pipeline.segment_chapters(layout, books, args.force, verbose))
    guard = gpu.Guard(lambda text: print(text, flush=True))
    if args.stage in ("draft", "run"):
        print(f"Drafting with {model} at {client.host}")

        def chat(system: str, user: str, schema: dict) -> dict:
            guard.before_call()
            return client.chat_json(model, system, user, schema, draft.NUM_CTX)

        reports.append(pipeline.draft_chapters(layout, books, chat, model, args.force, verbose))
    if args.stage in ("pictures", "run"):
        print(f"Reading figures with {vision_model} at {client.host}", flush=True)

        def ask(prompt: str, image: bytes, schema: dict, temperature: float) -> dict:
            guard.before_call()
            return client.read_image(vision_model, prompt, image, schema, temperature, render.NUM_CTX)

        reports.append(pipeline.picture_chapters(layout, books, ask, vision_model, args.force, verbose))
    if client is not None:
        print(guard.summary())

    for report in reports:
        print(report.line())
    return 1 if any(r.failed for r in reports) else 0


if __name__ == "__main__":
    sys.exit(main())
