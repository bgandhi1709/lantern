"""Stage 4: clean the extracted text and split a chapter into sections.

Cleaning removes what the printer put on every page (running headers, page numbers, InDesign
slugs, the reprint line) and the doubled text some PDFs use for bold headings. Splitting uses the
headings NCERT prints: numbered sections ("1.2 The Fundamental Theorem of Arithmetic"), exercises
("EXERCISE 1.2") and the summary. Books for the younger classes have no numbered headings, so they
fall back to one section per page. The splits only need to be good enough to anchor answers to a
place in the chapter; concepts come from the draft stage.
"""

from __future__ import annotations

import json
import re
from collections import Counter
from dataclasses import asdict, dataclass, field
from pathlib import Path

_PRINT_SLUG = re.compile(r"\.indd\s+\d+|^Reprint\s+\d{4}-\d{2}$|^[\d\s]{1,12}$|^PB$")
_SECTION = re.compile(r"^(\d{1,2}\.\d{1,2})\s+([A-Z][^.]{2,80})$")
_EXERCISE = re.compile(r"^EXERCISE\s+(\d{1,2}(?:\.\d{1,2})?)\b", re.IGNORECASE)
_SUMMARY = re.compile(r"^(?:\d{1,2}\.\d{1,2}\s+)?Summary$", re.IGNORECASE)
_DIGITS = re.compile(r"\d+")
_PAGE_NUMBERED = re.compile(r"^\d{1,3}\s+\S|\S\s+\d{1,3}$")
_EDGE_LINES = 3  # running headers and footers sit in the first or last few lines of a page


@dataclass
class Section:
    kind: str  # "intro" | "section" | "exercise" | "summary" | "page"
    number: str | None
    heading: str | None
    pages: list[int] = field(default_factory=list)
    text: str = ""


def undouble(line: str) -> str:
    """'Let us DoLet us Do' -> 'Let us Do'. Some PDFs draw bold text twice."""
    half, remainder = divmod(len(line), 2)
    if remainder == 0 and half >= 3 and line[:half] == line[half:]:
        return line[:half]
    return line


def _signature(line: str) -> str:
    return _DIGITS.sub("", line).strip().lower()


def running_lines(pages: list[list[str]], min_share: float = 0.3) -> set[str]:
    """Signatures of short lines that repeat at the top or bottom of many pages.

    A header carrying a page number ("2 MATHEMATICS", "REAL NUMBERS 9") often alternates between
    even and odd pages, so two repeats are enough for it. Any other line must repeat on at least
    three pages and 30% of them, so a heading such as "Let us Do" used twice is kept.
    """
    if len(pages) < 4:
        return set()
    counts: Counter[str] = Counter()
    numbered: set[str] = set()
    for lines in pages:
        edges = [l for l in lines[:_EDGE_LINES] + lines[-_EDGE_LINES:] if 0 < len(l) <= 60 and _signature(l)]
        counts.update({_signature(l) for l in edges})
        numbered.update(_signature(l) for l in edges if _PAGE_NUMBERED.search(l))
    threshold = max(3, int(len(pages) * min_share))
    return {s for s, count in counts.items() if count >= (2 if s in numbered else threshold)}


def strip_slugs(page_texts: list[str]) -> list[list[str]]:
    pages = [[undouble(l.strip()) for l in text.splitlines() if l.strip()] for text in page_texts]
    return [[l for l in lines if not _PRINT_SLUG.search(l)] for lines in pages]


def clean_pages(page_texts: list[str]) -> list[list[str]]:
    pages = strip_slugs(page_texts)
    repeated = running_lines(pages)
    cleaned = []
    for lines in pages:
        last = len(lines) - 1
        cleaned.append(
            [
                l
                for i, l in enumerate(lines)
                if not ((i < _EDGE_LINES or i > last - _EDGE_LINES) and _signature(l) in repeated and len(l) <= 60)
            ]
        )
    return cleaned


def guess_title(first_page: list[str]) -> str | None:
    parts = []
    for line in first_page[:4]:
        if _SECTION.match(line) or len(line) > 60 or re.match(r"^(Unit|Chapter)(\s+\d+)?$", line, re.IGNORECASE):
            if parts:
                break
            continue
        parts.append(line)
        if len(parts) == 2:
            break
    if not parts:
        return None
    # The chapter number is often glued to the title: "Furry Cat!1", "REAL NUMBERS 1".
    title = re.sub(r"\s*\d+$", "", " ".join(parts)).strip()
    return title or None


def split_sections(pages: list[list[str]]) -> list[Section]:
    has_headings = any(_SECTION.match(l) or _EXERCISE.match(l) for lines in pages for l in lines)
    if not has_headings:
        return [Section("page", str(n), None, [n], "\n".join(lines)) for n, lines in enumerate(pages, 1) if lines]

    sections = [Section("intro", None, None)]
    for number, lines in enumerate(pages, start=1):
        for line in lines:
            if _SUMMARY.match(line):
                sections.append(Section("summary", None, "Summary"))
            elif match := _EXERCISE.match(line):
                sections.append(Section("exercise", match.group(1), line))
            elif match := _SECTION.match(line):
                sections.append(Section("section", match.group(1), match.group(2).strip()))
            current = sections[-1]
            if number not in current.pages:
                current.pages.append(number)
            current.text += line + "\n"
    return [s for s in sections if s.text.strip()]


def segment(extracted: dict, chapter_id: str, book: dict) -> dict:
    texts = [p["text"] for p in extracted["pages"]]
    pages = clean_pages(texts)
    sections = split_sections(pages)
    # The title shares its line with the chapter number, which looks like a running header
    # ("REAL NUMBERS 1"), so guess it before headers are removed.
    first_page = strip_slugs(texts[:1])
    return {
        "chapter_id": chapter_id,
        "book_id": book["book_id"],
        "grade": book["grade"],
        "subject": book["subject"],
        "book_title": book["title"],
        "title_guess": guess_title(first_page[0]) if first_page else None,
        "pages_needing_vision": extracted.get("pages_needing_vision", []),
        "source_sha256": extracted.get("source_sha256"),
        "sections": [asdict(s) for s in sections],
    }


def write(chapter: dict, target: Path) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(chapter, indent=2, ensure_ascii=False), encoding="utf-8")
