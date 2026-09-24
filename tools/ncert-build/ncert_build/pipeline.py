"""Where each stage reads and writes, and the loop that runs a stage over the selected books.

Layout under the data directory (outside the repository):

    catalog.json                         every book NCERT lists
    pdf/<book_id>/<chapter_id>.pdf       downloads, plus <book_id>ps.pdf (front matter, contents)
    text/<book_id>/<chapter_id>.json     page text and quality flags
    chapters/<book_id>/<chapter_id>.json cleaned sections
    drafts/<book_id>/<chapter_id>.json   local-model concepts and kid questions
    bundles/<book_id>/<chapter_id>.md    compact input for Claude refinement
    refined/<book_id>/<chapter_id>.json  concept cards and Q&A written by Claude (ncert-refine)

A stage skips a chapter whose output already exists unless forced, so an interrupted run resumes
where it stopped. A chapter that fails is reported and the run carries on.
"""

from __future__ import annotations

import json
import time
import traceback
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Callable

from . import bundle, catalog, draft, extract, http, refine_check, segment
from .catalog import Book
from .config import NCERT_BASE, Settings


@dataclass
class Layout:
    root: Path

    @property
    def catalog(self) -> Path:
        return self.root / "catalog.json"

    def pdf(self, book_id: str, file_id: str) -> Path:
        return self.root / "pdf" / book_id / f"{file_id}.pdf"

    def text(self, book_id: str, chapter_id: str) -> Path:
        return self.root / "text" / book_id / f"{chapter_id}.json"

    def chapter(self, book_id: str, chapter_id: str) -> Path:
        return self.root / "chapters" / book_id / f"{chapter_id}.json"

    def draft(self, book_id: str, chapter_id: str) -> Path:
        return self.root / "drafts" / book_id / f"{chapter_id}.json"

    def bundle(self, book_id: str, chapter_id: str) -> Path:
        return self.root / "bundles" / book_id / f"{chapter_id}.md"

    def refined(self, book_id: str, chapter_id: str) -> Path:
        return self.root / "refined" / book_id / f"{chapter_id}.json"


@dataclass
class Report:
    stage: str
    done: int = 0
    skipped: int = 0
    missing: list[str] = field(default_factory=list)
    failed: list[str] = field(default_factory=list)

    def line(self) -> str:
        def listed(ids: list[str]) -> str:
            return f" ({', '.join(ids[:10])}{'…' if len(ids) > 10 else ''})" if ids else ""

        return (
            f"{self.stage}: {self.done} done, {self.skipped} skipped, "
            f"{len(self.missing)} missing{listed(self.missing)}, {len(self.failed)} failed{listed(self.failed)}"
        )


def _for_each_chapter(stage: str, books: list[Book], work: Callable[[Book, str], bool], verbose: bool) -> Report:
    report = Report(stage)
    for book in books:
        for chapter_id in book.chapter_ids():
            try:
                if work(book, chapter_id):
                    report.done += 1
                    if verbose:
                        print(f"  {stage} {chapter_id}")
                else:
                    report.skipped += 1
            except (http.NotFound, FileNotFoundError):
                # NCERT no longer serves the file (retired books still listed in the catalogue),
                # or an earlier stage has nothing for this chapter. Not an error in this stage.
                report.missing.append(chapter_id)
            except Exception as error:  # one bad chapter must not stop a long run
                report.failed.append(chapter_id)
                print(f"  ! {stage} {chapter_id}: {error}")
                if verbose:
                    traceback.print_exc()
    return report


def refresh_catalog(layout: Layout) -> list[Book]:
    html = http.fetch(f"{NCERT_BASE}/textbook.php").decode("utf-8", errors="replace")
    books = catalog.parse_textbook_page(html)
    catalog.save(books, layout.catalog)
    return books


def download(layout: Layout, settings: Settings, books: list[Book], verbose: bool) -> Report:
    def work(book: Book, chapter_id: str) -> bool:
        downloaded = http.download(f"{NCERT_BASE}/textbook/pdf/{chapter_id}.pdf", layout.pdf(book.book_id, chapter_id))
        if downloaded:
            time.sleep(settings.request_delay_seconds)
        return downloaded

    report = _for_each_chapter("download", books, work, verbose)
    for book in books:
        if book.has_front_matter:
            try:
                target = layout.pdf(book.book_id, book.front_matter_id)
                if http.download(f"{NCERT_BASE}/textbook/pdf/{book.front_matter_id}.pdf", target):
                    time.sleep(settings.request_delay_seconds)
            except http.NotFound:
                pass  # front matter is a nice-to-have (the contents page); chapters are what count
    return report


def extract_text(layout: Layout, books: list[Book], force: bool, verbose: bool) -> Report:
    def work(book: Book, chapter_id: str) -> bool:
        source, target = layout.pdf(book.book_id, chapter_id), layout.text(book.book_id, chapter_id)
        if not source.exists():
            raise FileNotFoundError("not downloaded")
        if target.exists() and not force:
            return False
        extract.write(source, extract.extract_pdf(source), target)
        return True

    return _for_each_chapter("extract", books, work, verbose)


def segment_chapters(layout: Layout, books: list[Book], force: bool, verbose: bool) -> Report:
    def work(book: Book, chapter_id: str) -> bool:
        source, target = layout.text(book.book_id, chapter_id), layout.chapter(book.book_id, chapter_id)
        if not source.exists():
            raise FileNotFoundError("not extracted")
        if target.exists() and not force:
            return False
        extracted = json.loads(source.read_text(encoding="utf-8"))
        segment.write(segment.segment(extracted, chapter_id, asdict(book)), target)
        return True

    return _for_each_chapter("segment", books, work, verbose)


def _stale_draft(path: Path) -> bool:
    """A draft made with an older prompt is redone, so a prompt change reaches every chapter."""
    try:
        return json.loads(path.read_text(encoding="utf-8"))["meta"]["prompt_version"] != draft.PROMPT_VERSION
    except (ValueError, KeyError):
        return True


def draft_chapters(
    layout: Layout, books: list[Book], chat: draft.ChatFn, model: str, force: bool, verbose: bool
) -> Report:
    def work(book: Book, chapter_id: str) -> bool:
        source, target = layout.chapter(book.book_id, chapter_id), layout.draft(book.book_id, chapter_id)
        if not source.exists():
            raise FileNotFoundError("not segmented")
        if target.exists() and not force and not _stale_draft(target):
            return False
        started = time.monotonic()
        result = draft.draft_chapter(json.loads(source.read_text(encoding="utf-8")), chat, model)
        result["meta"]["seconds"] = round(time.monotonic() - started, 1)
        draft.write(result, target)
        return True

    return _for_each_chapter("draft", books, work, verbose)


def bundle_chapters(layout: Layout, books: list[Book], force: bool, verbose: bool) -> Report:
    def work(book: Book, chapter_id: str) -> bool:
        source, target = layout.chapter(book.book_id, chapter_id), layout.bundle(book.book_id, chapter_id)
        if not source.exists():
            raise FileNotFoundError("not segmented")
        if target.exists() and not force:
            return False
        draft_path = layout.draft(book.book_id, chapter_id)
        drafted = json.loads(draft_path.read_text(encoding="utf-8")) if draft_path.exists() else None
        chapter = json.loads(source.read_text(encoding="utf-8"))
        bundle.write(bundle.render(chapter, drafted, str(layout.refined(book.book_id, chapter_id))), target)
        return True

    return _for_each_chapter("bundle", books, work, verbose)


def check_refined(layout: Layout, book: Book, chapter_id: str) -> list[str]:
    target = layout.refined(book.book_id, chapter_id)
    if not target.exists():
        return [f"no refined file at {target}"]
    try:
        refined = json.loads(target.read_text(encoding="utf-8"))
    except ValueError as error:
        return [f"not valid JSON: {error}"]
    chapter = json.loads(layout.chapter(book.book_id, chapter_id).read_text(encoding="utf-8"))
    page_count = max((n for s in chapter["sections"] for n in s["pages"]), default=0)
    return refine_check.check(refined, chapter_id, page_count)


def status(layout: Layout, books: list[Book]) -> list[tuple[str, int, int, int, int, int, int]]:
    rows = []
    for book in books:
        ids = book.chapter_ids()
        rows.append(
            (
                f"{book.book_id} C{book.grade} {book.subject}: {book.title}",
                len(ids),
                sum(layout.pdf(book.book_id, c).exists() for c in ids),
                sum(layout.text(book.book_id, c).exists() for c in ids),
                sum(layout.chapter(book.book_id, c).exists() for c in ids),
                sum(layout.draft(book.book_id, c).exists() for c in ids),
                sum(layout.refined(book.book_id, c).exists() for c in ids),
            )
        )
    return rows
