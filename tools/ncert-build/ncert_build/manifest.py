"""Stage 7: the Class manifest, one unified file per Board and Class (ADR-0014).

The Board's rules (the order and icons of its Subjects, where its Chapter titles come from) run here, once, so the API reads a
ready-made list and has no Board logic. The manifest is made from what the build already produced:

    board.json                         the Board profile
    catalog.json                       the Books the Board lists
    refined/<book>/<chapter>.json      a Chapter is built when its refined file exists
    summaries/<book>/<chapter>.json    the Chapter summary shown to the Parent
    toc/<book>.json                    SSC: each Chapter's title

and written to ``manifest/class-<N>.json``. ``upload`` copies it to the Board's container; the API's copy puts it in each Child's
slice as ``content/manifest.json``. A Class either gets its whole manifest or none: a Chapter that cannot be listed fails the
Class and nothing is written for it.
"""

from __future__ import annotations

import json
import re
from pathlib import Path

from . import catalog, pipeline
from .catalog import Book
from .pipeline import Layout, Report

SCHEMA = "class-manifest-v1"
# Subjects the profile does not list sort after the ones it does, by name, with this icon.
UNLISTED_ICON = "book"
# NCERT lists every Book in every medium and builds some: a Book with nothing refined is simply not built. An SSC Book is only
# listed once it is built, so one with nothing refined is a broken master (the same rule the API's copy applies, D89).
_SKIPS_UNBUILT = {"cbse"}


class ManifestError(Exception):
    """A Class cannot be listed: the message says which Book or Chapter, so the build can be fixed."""


def write_all(layout: Layout, board: str, grades: set[int] | None = None) -> Report:
    """Writes ``manifest/class-N.json`` for each selected Class of the Board that has a built Book.

    The Board's current profile is written to board.json first, so the file ``upload`` sends is the one the manifests follow,
    also when the data was restored from the container and carries an older (or no) board.json.
    """
    pipeline.write_profile(layout, board)
    profile = pipeline.profile_for(board)
    books = catalog.load(layout.catalog)
    report = Report("manifest")
    for grade in profile["classes"]:
        if grades is not None and grade not in grades:
            continue
        label = f"class-{grade}"
        try:
            document = build_class(layout, profile, [book for book in books if book.grade == grade], grade)
        except ManifestError as error:
            report.failed.append(label)
            print(f"  ! manifest {label}: {error}")
            continue
        if document is None:
            report.missing.append(label)
            continue
        target = layout.manifest(grade)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(document, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        report.done += 1
    return report


def build_class(layout: Layout, profile: dict, books: list[Book], grade: int) -> dict | None:
    """The manifest of one Class, or None when none of its Books is built."""
    by_subject: dict[str, list[dict]] = {}
    for book in sorted(books, key=lambda b: (b.part or 0, b.book_id)):
        chapters = _chapters(layout, profile, book)
        if chapters is None:
            if profile["board"] in _SKIPS_UNBUILT:
                continue
            raise ManifestError(f"Book {book.book_id} is listed for Class {grade} but has no refined Chapters")
        by_subject.setdefault(book.subject, []).extend(chapters)
    if not by_subject:
        return None

    listed = {entry["name"]: (position, entry["icon"]) for position, entry in enumerate(profile["subjects"])}
    order = sorted(by_subject, key=lambda name: (listed.get(name, (len(listed), ""))[0], name.casefold()))
    return {
        "schema": SCHEMA,
        "board": profile["board"],
        "profileVersion": profile["profile_version"],
        "class": grade,
        "subjects": [
            {
                "subjectId": _slug(name),
                "name": name,
                "icon": listed[name][1] if name in listed else UNLISTED_ICON,
                "chapterCount": len(by_subject[name]),
                "chapters": by_subject[name],
            }
            for name in order
        ],
    }


def _chapters(layout: Layout, profile: dict, book: Book) -> list[dict] | None:
    """The Book's built Chapters in book order, or None when it has none."""
    built = sorted(layout.refined(book.book_id, "x").parent.glob("*.json"))
    if not built:
        return None
    titles = _toc_titles(layout, book) if profile["chapter_titles"] == "toc" else None
    chapters = []
    for refined_path in built:
        chapter_id = refined_path.stem
        number = _numbered(book, chapter_id)
        if titles is not None:
            title = titles.get(chapter_id) or _fail(f"Chapter {chapter_id} is not in the contents file of {book.book_id}")
        else:
            title = _read(refined_path).get("chapter_title")
        summary = _read(layout.root / "summaries" / book.book_id / f"{chapter_id}.json", chapter_id).get("summary")
        if not isinstance(title, str) or not title.strip():
            raise ManifestError(f"Chapter {chapter_id} has no title")
        if not isinstance(summary, str) or not summary.strip():
            raise ManifestError(f"Chapter {chapter_id} has no summary")
        chapters.append(
            {"chapterId": chapter_id, "bookId": book.book_id, "number": number, "title": title.strip(), "summary": summary.strip()}
        )
    return sorted(chapters, key=lambda chapter: (chapter["number"], chapter["chapterId"]))


def _numbered(book: Book, chapter_id: str) -> int:
    """The number is what follows the Book's id in the Chapter's: ``cesa101`` in ``cesa1`` and ``ssc3-maths-04`` in ``ssc3-maths`` are 1 and 4.

    The contents file's own ``number`` is the label as printed ("1", "s1", "No. 2"), so it is not used.
    """
    rest = chapter_id[len(book.book_id) :].removeprefix("-") if chapter_id.startswith(book.book_id) else ""
    if not rest.isdigit() or int(rest) < 1:
        raise ManifestError(f"Chapter {chapter_id} is not named after its Book {book.book_id}")
    return int(rest)


def _toc_titles(layout: Layout, book: Book) -> dict[str, str]:
    path = layout.toc(book.book_id)
    if not path.exists():
        raise ManifestError(f"Book {book.book_id} has no contents file ({path.name})")
    return {entry["chapter_id"]: entry.get("title") for entry in json.loads(path.read_text(encoding="utf-8"))["chapters"]}


def _read(path: Path, chapter_id: str | None = None) -> dict:
    if not path.exists():
        raise ManifestError(f"Chapter {chapter_id or path.stem} has no file {path.parent.name}/{path.name}")
    return json.loads(path.read_text(encoding="utf-8"))


def _fail(message: str) -> str:
    raise ManifestError(message)


def _slug(name: str) -> str:
    return re.sub(r"[^a-z0-9]+", "-", name.lower()).strip("-")
