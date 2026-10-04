"""Where each stage reads and writes, and the loop that runs a stage over the selected books.

Layout under the data directory (outside the repository):

    board.json                           the Board profile (SSC): how its Books are built
    catalog.json                         every book the Board lists
    pdf/<book_id>/<chapter_id>.pdf       downloads, plus <book_id>ps.pdf (front matter, contents);
                                         for SSC the whole Book <book_id>.pdf, split into Chapters
    toc/<book_id>.json                   SSC: the Book's Chapters and their page ranges (split)
    guidance/<book_id>.json              SSC: Book guidance, the notes for teachers and parents
    text/<book_id>/<chapter_id>.json     page text and quality flags
    chapters/<book_id>/<chapter_id>.json cleaned sections
    drafts/<book_id>/<chapter_id>.json   local-model concepts and kid questions
    pictures/<book_id>/<chapter_id>.json local vision model's description of each picture page
    pictures/<book_id>/<chapter_id>/pN.png  the rendered picture pages (low_text)
    bundles/<book_id>/<chapter_id>.md    compact input for Claude refinement
    refined/<book_id>/<chapter_id>.json  concept cards and Q&A written by Claude (ncert-refine)

A stage skips a chapter whose output already exists unless forced, so an interrupted run resumes
where it stopped. A chapter that fails is reported and the run carries on.
"""

from __future__ import annotations

import json
import time
import traceback
from dataclasses import asdict, dataclass, field, replace
from pathlib import Path
from typing import Callable

from pypdf import PdfReader

from . import bundle, catalog, draft, extract, guidance, http, refine_check, render, segment, split, ssc
from .catalog import Book
from .config import NCERT_BASE, Settings


@dataclass
class Layout:
    root: Path

    @property
    def catalog(self) -> Path:
        return self.root / "catalog.json"

    @property
    def board_profile(self) -> Path:
        return self.root / "board.json"

    def book_pdf(self, book_id: str) -> Path:
        return self.root / "pdf" / book_id / f"{book_id}.pdf"

    def toc(self, book_id: str) -> Path:
        return self.root / "toc" / f"{book_id}.json"

    def guidance(self, book_id: str) -> Path:
        return self.root / "guidance" / f"{book_id}.json"

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

    def pictures(self, book_id: str, chapter_id: str) -> Path:
        return self.root / "pictures" / book_id / f"{chapter_id}.json"

    def picture_images(self, book_id: str, chapter_id: str) -> Path:
        return self.root / "pictures" / book_id / chapter_id

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


# Set by the command line's --chapters: limits every stage to those chapters.
only_chapters: set[str] | None = None


def _for_each_chapter(stage: str, books: list[Book], work: Callable[[Book, str], bool], verbose: bool) -> Report:
    report = Report(stage)
    for book in books:
        for chapter_id in book.chapter_ids():
            if only_chapters is not None and chapter_id not in only_chapters:
                continue
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


def refresh_catalog(layout: Layout, board: str) -> list[Book]:
    if board == "ssc":
        books = ssc.fetch_catalog()
        layout.board_profile.parent.mkdir(parents=True, exist_ok=True)
        layout.board_profile.write_text(json.dumps(ssc.PROFILE, indent=2, ensure_ascii=False), encoding="utf-8")
    else:
        html = http.fetch(f"{NCERT_BASE}/textbook.php").decode("utf-8", errors="replace")
        books = catalog.parse_textbook_page(html)
    catalog.save(books, layout.catalog)
    return books


def download_books(layout: Layout, settings: Settings, books: list[Book], verbose: bool) -> Report:
    """Whole-Book PDFs (SSC); the split stage makes the Chapter PDFs."""
    report = Report("download")
    for book in books:
        try:
            if http.download(book.pdf_url, layout.book_pdf(book.book_id)):
                report.done += 1
                time.sleep(settings.request_delay_seconds)
                if verbose:
                    print(f"  download {book.book_id}")
            else:
                report.skipped += 1
        except Exception as error:
            report.failed.append(book.book_id)
            print(f"  ! download {book.book_id}: {error}")
    return report


def split_books(
    layout: Layout, books: list[Book], chat: split.ChatFn, ask: split.AskFn, model: str, force: bool, verbose: bool
) -> Report:
    """Splits each whole-Book PDF into Chapter PDFs and records the Chapters in the catalogue."""
    report = Report("split")
    profile = json.loads(layout.board_profile.read_text(encoding="utf-8"))
    every_book = {b.book_id: b for b in catalog.load(layout.catalog)}
    for book in books:
        source, target = layout.book_pdf(book.book_id), layout.toc(book.book_id)
        try:
            if not source.exists():
                report.missing.append(book.book_id)
                continue
            if target.exists() and not force:
                report.skipped += 1
                continue
            unit = profile["chapter_unit"].get(book.subject, "lesson")
            toc = split.split_book(source, book, unit, chat, ask, model)
            split.write_chapters(source, toc, lambda chapter_id: layout.pdf(book.book_id, chapter_id))
            split.write_toc(toc, target)
            every_book[book.book_id] = replace(
                book, last_chapter=len(toc["chapters"]), source_sha256=toc["source_sha256"]
            )
            report.done += 1
            if verbose:
                print(f"  split {book.book_id}: {len(toc['chapters'])} chapters")
        except Exception as error:  # one bad Book must not stop the run
            report.failed.append(book.book_id)
            print(f"  ! split {book.book_id}: {error}")
            if verbose:
                traceback.print_exc()
    catalog.save(sorted(every_book.values(), key=lambda b: (b.grade, b.subject, b.book_id)), layout.catalog)
    return report


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


def guidance_books(
    layout: Layout, books: list[Book], chat: guidance.ChatFn, model: str, force: bool, verbose: bool
) -> Report:
    """One Book guidance file per split Book; a note not found on its page is reported, not kept."""
    report = Report("guidance")
    for book in books:
        source, toc_path, target = layout.book_pdf(book.book_id), layout.toc(book.book_id), layout.guidance(book.book_id)
        try:
            if not source.exists() or not toc_path.exists():
                report.missing.append(book.book_id)
                continue
            if target.exists() and not force:
                report.skipped += 1
                continue
            texts = [page.extract_text() or "" for page in PdfReader(source).pages]
            toc = json.loads(toc_path.read_text(encoding="utf-8"))
            document, rejected = guidance.extract(book.book_id, book.title, texts, toc, chat, model)
            guidance.write(document, target)
            report.done += 1
            for line in rejected:
                print(f"  ~ guidance {book.book_id} rejected {line}")
            if verbose:
                print(f"  guidance {book.book_id}: {len(document['book_notes'])} book, {len(document['chapter_notes'])} chapter notes")
        except Exception as error:
            report.failed.append(book.book_id)
            print(f"  ! guidance {book.book_id}: {error}")
    return report


def _stale_text(path: Path) -> bool:
    try:
        return json.loads(path.read_text(encoding="utf-8")).get("version") != extract.EXTRACT_VERSION
    except ValueError:
        return True


def _older(target: Path, source: Path) -> bool:
    """A later stage is redone when the stage before it rewrote its input."""
    return target.stat().st_mtime < source.stat().st_mtime


def extract_text(layout: Layout, books: list[Book], force: bool, verbose: bool) -> Report:
    def work(book: Book, chapter_id: str) -> bool:
        source, target = layout.pdf(book.book_id, chapter_id), layout.text(book.book_id, chapter_id)
        if not source.exists():
            raise FileNotFoundError("not downloaded")
        if target.exists() and not force and not _stale_text(target):
            return False
        extract.write(source, extract.extract_pdf(source), target)
        return True

    return _for_each_chapter("extract", books, work, verbose)


def segment_chapters(layout: Layout, books: list[Book], force: bool, verbose: bool) -> Report:
    def work(book: Book, chapter_id: str) -> bool:
        source, target = layout.text(book.book_id, chapter_id), layout.chapter(book.book_id, chapter_id)
        if not source.exists():
            raise FileNotFoundError("not extracted")
        if target.exists() and not force and not _older(target, source):
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
        pictures_path = layout.pictures(book.book_id, chapter_id)
        pictures = json.loads(pictures_path.read_text(encoding="utf-8")).get("figures", []) if pictures_path.exists() else []
        text = bundle.render(chapter, drafted, str(layout.refined(book.book_id, chapter_id)), pictures)
        bundle.write(text, target)
        return True

    return _for_each_chapter("bundle", books, work, verbose)


def _stale_pictures(path: Path, model: str) -> bool:
    try:
        meta = json.loads(path.read_text(encoding="utf-8"))
        return meta["prompt_version"] != render.PROMPT_VERSION or meta["model"] != model
    except (ValueError, KeyError):
        return True


def picture_chapters(
    layout: Layout, books: list[Book], ask: render.AskFn, model: str, force: bool, verbose: bool
) -> Report:
    """Finds each chapter's figures and has the local vision model read them (see render)."""

    def work(book: Book, chapter_id: str) -> bool:
        source, target = layout.pdf(book.book_id, chapter_id), layout.pictures(book.book_id, chapter_id)
        if not source.exists():
            raise FileNotFoundError("not downloaded")
        if target.exists() and not force and not _stale_pictures(target, model):
            return False
        image_dir = layout.picture_images(book.book_id, chapter_id)
        for stale in image_dir.glob("p*.png"):  # a redo must not leave images it no longer lists
            stale.unlink()
        started = time.monotonic()
        reader = render.Reader(ask, book.grade, book.subject)
        figures = render.read_chapter(source, reader, image_dir)
        target.parent.mkdir(parents=True, exist_ok=True)
        document = {
            "model": model,
            "prompt_version": render.PROMPT_VERSION,
            "seconds": round(time.monotonic() - started, 1),
            "figures": figures,
        }
        target.write_text(json.dumps(document, indent=2, ensure_ascii=False), encoding="utf-8")
        if verbose:
            unsure = sum(1 for f in figures for g in f.get("counts", []) if not g["sure"])
            print(f"    {len(figures)} figures in {document['seconds']} s, {unsure} unsure counts")
        return True

    return _for_each_chapter("pictures", books, work, verbose)


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
    text_path = layout.text(book.book_id, chapter_id)
    if text_path.exists():  # a picture page with no text has no section but can still be cited
        page_count = max(page_count, len(json.loads(text_path.read_text(encoding="utf-8"))["pages"]))
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
