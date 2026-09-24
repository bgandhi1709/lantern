"""Stage 1: read NCERT's book list out of textbook.php.

The page has no API. Its book list lives in JavaScript that fills three dropdowns: a condition per
class and subject, then option text (the title) and value (``textbook.php?aejm1=0-13``). The value
encodes the book id and the chapter range; chapter PDFs are then ``textbook/pdf/aejm101.pdf`` and
the front matter with the contents page is ``aejm1ps.pdf``.
"""

from __future__ import annotations

import json
import re
from dataclasses import asdict, dataclass
from pathlib import Path

_CONDITION = re.compile(
    r'tclass\.value==(\d+)\)\s*&&\s*\(document\.test\.tsubject\.options\[sind\]\.text=="([^"]+)"'
)
_OPTION = re.compile(r'tbook\.options\[(\d+)\]\.(text|value)="([^"]*)"')
_VALUE = re.compile(r"^textbook\.php\?([a-z]+)(\d+)=(\d+)-(\d+)$")
_TITLE_LANGUAGE = re.compile(r"\(([A-Za-z]+)\)\s*$")

# The second letter of a four-letter code is the medium; longer codes are regional editions whose
# language is named in the title, e.g. "Joyful-Mathematics (Gujarati)".
_MEDIUM_BY_CODE_LETTER = {"e": "English", "h": "Hindi", "u": "Urdu"}


@dataclass(frozen=True)
class Book:
    book_id: str  # e.g. "aejm1": code + book number; unique
    grade: int
    subject: str
    title: str
    medium: str
    first_chapter: int
    last_chapter: int
    has_front_matter: bool

    def chapter_ids(self) -> list[str]:
        return [f"{self.book_id}{n:02d}" for n in range(self.first_chapter, self.last_chapter + 1)]

    @property
    def front_matter_id(self) -> str:
        return f"{self.book_id}ps"


def _medium(code: str, title: str) -> str | None:
    named = _TITLE_LANGUAGE.search(title)
    if named:
        return named.group(1)
    if len(code) == 4:
        return _MEDIUM_BY_CODE_LETTER.get(code[1])
    return None


def parse_textbook_page(html: str) -> list[Book]:
    """Returns every book the page lists, skipping commented-out entries and duplicates."""
    current: tuple[int, str] | None = None
    options: dict[tuple[tuple[int, str], int], dict[str, str]] = {}

    for raw in html.splitlines():
        line = raw.strip()
        if line.startswith("//"):
            continue
        condition = _CONDITION.search(line)
        if condition:
            current = (int(condition.group(1)), condition.group(2).strip())
            continue
        option = _OPTION.search(line)
        if option and current:
            options.setdefault((current, int(option.group(1))), {})[option.group(2)] = option.group(3)

    books: dict[str, Book] = {}
    for ((grade, subject), _), fields in options.items():
        value = _VALUE.match(fields.get("value", ""))
        if not value:
            continue
        code, number, start, end = value.group(1), value.group(2), int(value.group(3)), int(value.group(4))
        title = fields.get("text", "").strip()
        medium = _medium(code, title)
        if medium is None:
            continue
        book_id = f"{code}{number}"
        books.setdefault(
            book_id,
            Book(
                book_id=book_id,
                grade=grade,
                subject=subject,
                title=title,
                medium=medium,
                first_chapter=max(start, 1),
                last_chapter=end,
                has_front_matter=start == 0,
            ),
        )
    return sorted(books.values(), key=lambda b: (b.grade, b.subject, b.book_id))


def select(
    books: list[Book],
    grades: set[int] | None = None,
    subjects: set[str] | None = None,
    mediums: set[str] | None = None,
    book_ids: set[str] | None = None,
) -> list[Book]:
    def keep(book: Book) -> bool:
        if book_ids and book.book_id not in book_ids:
            return False
        if grades and book.grade not in grades:
            return False
        if subjects and book.subject not in subjects:
            return False
        if mediums and book.medium not in mediums:
            return False
        return True

    return [b for b in books if keep(b)]


def save(books: list[Book], path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps([asdict(b) for b in books], indent=2, ensure_ascii=False), encoding="utf-8")


def load(path: Path) -> list[Book]:
    if not path.exists():
        raise SystemExit(f"No catalogue at {path}. Run `ncert-build catalog` first.")
    return [Book(**row) for row in json.loads(path.read_text(encoding="utf-8"))]
