"""Split stage: one whole-Book PDF into per-Chapter PDFs, from the Book's contents page.

The contents page is found by its shape (a heading such as Contents, Index or Lesson's Name, then
rows ending in increasing page numbers). The local model turns it into Chapters; when it has no
text layer the local vision model reads it instead. Python then trusts nothing the model said
without checking: the offset between printed and PDF page numbers comes from the page numbers on
the pages themselves, and every Chapter's title (an English Unit's first lesson) must be on the
page it is placed at.
"""

from __future__ import annotations

import difflib
import hashlib
import io
import json
import re
from collections import Counter
from pathlib import Path
from typing import Callable

import pypdfium2 as pdfium
from pypdf import PdfReader, PdfWriter

from .catalog import Book

PROMPT_VERSION = "toc-v1"
NUM_CTX = 8192
FRONT_PAGES = 25
_THRESHOLD = 15  # a heading (10) and a run of at least five rows
_PLACE_WINDOW = 2

ChatFn = Callable[[str, str, dict], dict]
AskFn = Callable[[str, bytes, dict], dict]

_ROW = re.compile(r"\b(\d{1,3})(?:\s*[-–,]\s*\d{1,3})?\s*$")
_HEADING = re.compile(r"\b(contents|index|lesson.?s name|name of the lesson|chapter name|lesson name)\b", re.I)

_ENTRY = {
    "type": "object",
    "properties": {"number": {"type": "string"}, "title": {"type": "string"}, "printed_page": {"type": "integer"}},
    "required": ["number", "title", "printed_page"],
}
SCHEMA = {
    "type": "object",
    "properties": {
        "chapters": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {**_ENTRY["properties"], "lessons": {"type": "array", "items": _ENTRY}},
                "required": ["number", "title", "printed_page", "lessons"],
            },
        }
    },
    "required": ["chapters"],
}


class SplitError(Exception):
    pass


def contents_score(text: str) -> int:
    pages = [int(m.group(1)) for line in text.splitlines() if (m := _ROW.search(line.strip())) and re.search(r"[A-Za-z]{3}", line)]
    run = best = 0
    previous = -1
    for page in pages:
        run = run + 1 if page >= previous else 1
        previous = page
        best = max(best, run)
    return best + (10 if _HEADING.search(text) else 0)


def find_contents(texts: list[str]) -> int | None:
    """Index of the contents page among the front pages, or None (no text layer there)."""
    scored = [(contents_score(text), index) for index, text in enumerate(texts[:FRONT_PAGES])]
    score, index = max(scored, default=(0, 0))
    return index if score >= _THRESHOLD else None


def _printed_number(text: str) -> set[int]:
    """Candidate page numbers: bare numbers near the top or bottom of the page."""
    tokens = text.split()
    return {int(t) for t in tokens[:12] + tokens[-6:] if t.isdigit()}


def printed_number_offset(texts: list[str], after: int) -> int:
    """PDF index minus printed page number, voted over the pages after the contents."""
    votes: Counter[int] = Counter()
    for index in range(after + 1, len(texts)):
        for number in _printed_number(texts[index]):
            if 0 < number <= 400 and index - number >= 0:
                votes[index - number] += 1
    if not votes:
        raise SplitError("no printed page numbers found")
    return votes.most_common(1)[0][0]


def _compact(text: str) -> str:
    return re.sub(r"[^a-z0-9]", "", text.lower())


def _word_on_page(word: str, page: str, page_words: list[str]) -> bool:
    # Printed titles carry typos ("Commnuity", "Introducting") that the model quietly corrects.
    return word in page or bool(difflib.get_close_matches(word, page_words, n=1, cutoff=0.8))


def title_on_page(title: str, text: str) -> bool:
    if _compact(title) and _compact(title) in _compact(text):
        return True
    words = re.findall(r"[a-z]{3,}", title.lower())
    page, page_words = _compact(text), re.findall(r"[a-z]{3,}", text.lower())
    return bool(words) and sum(_word_on_page(w, page, page_words) for w in words) / len(words) >= 0.7


def _is_chapter(entry: dict) -> bool:
    """The model sometimes lists the page's heading or furniture ("INDEX", a map disclaimer) as a
    chapter; those have no number."""
    return (
        entry["printed_page"] >= 1
        and bool(entry["number"].strip())
        and not _HEADING.fullmatch(entry["title"].strip(" *.:"))
    )


def _anchor(chapter: dict) -> str:
    return chapter["lessons"][0]["title"] if chapter["lessons"] else chapter["title"]


def place(chapters: list[dict], texts: list[str], offset: int, after: int) -> list[dict]:
    """Gives each Chapter its PDF page range (1-based), checking its title is where it lands.
    Only pages after the contents page count: that page lists every title."""
    starts, checked = [], []
    for n, chapter in enumerate(chapters):
        guess = chapter["printed_page"] + offset
        title = _anchor(chapter)
        start = next(
            (
                index
                for delta in sorted(range(-_PLACE_WINDOW, _PLACE_WINDOW + 1), key=abs)
                if after < (index := guess + delta) < len(texts) and title_on_page(title, texts[index])
            ),
            None,
        )
        if start is None:
            # A mis-paired page number (a contents page printed in columns): scan forward to the
            # next Chapter's page for the title.
            after_previous = max(after, starts[-1] if starts else after) + 1
            until = chapters[n + 1]["printed_page"] + offset + _PLACE_WINDOW if n + 1 < len(chapters) else len(texts) - 1
            start = next((i for i in range(after_previous, min(until, len(texts) - 1) + 1) if title_on_page(title, texts[i])), None)
        title_checked = start is not None
        if start is None and 0 <= guess < len(texts) and chapter["printed_page"] in _printed_number(texts[guess]):
            start = guess  # the heading is drawn, not text; the printed page number still agrees
        if start is None:
            raise SplitError(f"chapter {chapter['number']} {title!r} not found near PDF page {guess + 1}")
        starts.append(start)
        checked.append(title_checked)
    if any(b <= a for a, b in zip(starts, starts[1:])):
        raise SplitError(f"chapters out of order: {starts}")
    last_numbered = max(
        (i for i, text in enumerate(texts) if i - offset in _printed_number(text)),
        default=len(texts) - 1,
    )
    placed = []
    for n, (chapter, start, title_checked) in enumerate(zip(chapters, starts, checked)):
        end = starts[n + 1] - 1 if n + 1 < len(starts) else max(last_numbered, start)
        placed.append(
            {
                **chapter,
                "printed_pages": [chapter["printed_page"], end - offset],
                "pdf_pages": [start + 1, end + 1],
                "title_checked": title_checked,
            }
        )
    return placed


def columns(text: str) -> list[dict] | None:
    """A contents page printed as columns (every title, then every serial number, then every page
    number) reads back as three blocks the model mis-pairs; they pair exactly in order."""
    titles: list[str] = []
    numbers: list[int] = []
    for line in (raw.strip() for raw in text.splitlines()):
        if not line or _HEADING.search(line) or re.search(r"\b(sr\.?\s*no|page\s*no)", line, re.I):
            continue
        if line.isdigit():
            numbers.append(int(line))
        elif numbers:
            return None  # text after the numbers: not a column layout
        elif titles and line[0].islower():
            titles[-1] += " " + line  # a title wrapped onto a second line
        else:
            titles.append(line)
    count = len(titles)
    serials, pages = numbers[:count], numbers[count:]
    if count < 3 or serials != list(range(1, count + 1)) or len(pages) != count or pages != sorted(pages):
        return None
    return [
        {"number": str(n), "title": title, "printed_page": page, "lessons": []}
        for n, (title, page) in enumerate(zip(titles, pages), start=1)
    ]


def _prompt(book: Book, unit: str) -> str:
    grouping = (
        "The page groups lessons into units (UNIT ONE, then 1.1, 1.2 …). Each unit is one chapter: "
        "number it 1, 2 …, title it like 'Unit One', and list its lessons with their numbers, titles and pages."
        if unit == "unit"
        else "Each numbered lesson is one chapter; leave lessons empty."
    )
    return (
        f"This is the contents page of a Maharashtra State Board Standard {book.grade} {book.subject} "
        f"textbook ({book.title}). List every chapter in book order with its number, its title exactly as "
        f"printed, and the printed page it starts on. {grouping} Leave out anything that is not a chapter."
    )


def read_contents(
    book: Book, unit: str, texts: list[str], pdf_path: Path, chat: ChatFn, ask: AskFn
) -> tuple[list[dict], int, bool]:
    """Chapters from the contents page, the page's index, and whether its text checked the titles."""
    index = find_contents(texts)
    if index is not None:
        paired = columns(texts[index])
        if paired:
            return paired, index, True
        result = chat(_prompt(book, unit), texts[index], SCHEMA)
        chapters = [c for c in result["chapters"] if _is_chapter(c)]
        missing = [c["title"] for c in chapters if not title_on_page(c["title"], texts[index])]
        missing += [l["title"] for c in chapters for l in c["lessons"] if not title_on_page(l["title"], texts[index])]
        if missing:
            raise SplitError(f"titles not on the contents page: {missing[:5]}")
        return chapters, index, True
    # No text layer: the vision model reads every image-only front page, and the one giving the
    # most chapters with increasing pages is the contents page. (Telling it to return nothing for
    # other pages made it return nothing for the contents page too.)
    best: tuple[list[dict], int] | None = None
    document = pdfium.PdfDocument(pdf_path)
    try:
        for index, text in enumerate(texts[:FRONT_PAGES]):
            if text.strip():
                continue
            image = io.BytesIO()
            # A larger render overflows the vision context and comes back empty.
            document[index].render(scale=1.4).to_pil().save(image, format="PNG")
            reply = ask(_prompt(book, unit), image.getvalue(), SCHEMA)["chapters"]
            chapters = [c for c in reply if _is_chapter(c) and c["printed_page"] < len(texts)]
            pages = [c["printed_page"] for c in chapters]
            if len(chapters) >= 3 and pages == sorted(pages) and (best is None or len(chapters) > len(best[0])):
                best = (chapters, index)
    finally:
        document.close()
    if best:
        return best[0], best[1], False
    raise SplitError("no contents page found")


def split_book(pdf_path: Path, book: Book, unit: str, chat: ChatFn, ask: AskFn, model: str) -> dict:
    texts = [page.extract_text() or "" for page in PdfReader(pdf_path).pages]
    chapters, contents_index, checked = read_contents(book, unit, texts, pdf_path, chat, ask)
    for n, chapter in enumerate(chapters, start=1):
        chapter["chapter_id"] = f"{book.book_id}-{n:02d}"
    offset = printed_number_offset(texts, contents_index)
    placed = place(chapters, texts, offset, contents_index)
    covered = {p for c in placed for p in range(c["pdf_pages"][0], c["pdf_pages"][1] + 1)}
    return {
        "book_id": book.book_id,
        "contents_pdf_page": contents_index + 1,
        "page_offset": offset,
        "chapters": [
            {
                "chapter_id": c["chapter_id"],
                "number": c["number"],
                "title": c["title"],
                "printed_pages": c["printed_pages"],
                "pdf_pages": c["pdf_pages"],
                "title_checked": c["title_checked"],
                "lessons": c["lessons"],
            }
            for c in placed
        ],
        "unassigned_pdf_pages": [p for p in range(1, len(texts) + 1) if p not in covered],
        "source_sha256": hashlib.sha256(pdf_path.read_bytes()).hexdigest(),
        "meta": {"model": model, "prompt_version": PROMPT_VERSION, "contents_text_checked": checked, "verified": True},
    }


def write_chapters(pdf_path: Path, toc: dict, chapter_pdf: Callable[[str], Path]) -> None:
    reader = PdfReader(pdf_path)
    for chapter in toc["chapters"]:
        writer = PdfWriter()
        first, last = chapter["pdf_pages"]
        for index in range(first - 1, last):
            writer.add_page(reader.pages[index])
        target = chapter_pdf(chapter["chapter_id"])
        target.parent.mkdir(parents=True, exist_ok=True)
        with target.open("wb") as handle:
            writer.write(handle)


def write_toc(toc: dict, target: Path) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(toc, indent=2, ensure_ascii=False), encoding="utf-8")
