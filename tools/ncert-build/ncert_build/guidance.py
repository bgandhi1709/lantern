"""Book guidance: the rights holder's own notes for teachers and parents (D57).

Balbharati Books print them in the front matter ("Instructions for Teachers", "For Teachers and
Parents") and in boxes beside lessons ("For Teachers : …"). Pages that carry one are found by their
heading; the local model copies each note out, because a box's edges are lost in the text layer.
A note is kept only if its words are on the page in the same order, so the model can trim a note
but never add to one.
"""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Callable

PROMPT_VERSION = "guidance-v1"
NUM_CTX = 8192
_MIN_CHARS = 40  # shorter is a stray heading, not a note

ChatFn = Callable[[str, str, dict], dict]

HEADING = re.compile(
    r"(instructions?|guidelines?|notes?)\s+(for|to)\s+(the\s+)?(teachers?|parents?)"
    r"|\bfor\s+(the\s+)?teachers?(\s+and\s+parents?)?\s*:?"
    r"|\bto\s+the\s+teachers?\b",
    re.I,
)

SCHEMA = {
    "type": "object",
    "properties": {
        "notes": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {"heading": {"type": "string"}, "text": {"type": "string"}},
                "required": ["heading", "text"],
            },
        }
    },
    "required": ["notes"],
}

PROMPT = (
    "This is one page of a school textbook. Copy out every note on it that is addressed to teachers "
    "or parents, such as 'Instructions for Teachers', 'For Teachers' or 'For Teachers and Parents'. "
    "For each, give its heading and its text exactly as printed, word for word, without the heading. "
    "Do not summarise or fix anything. Leave out lesson content meant for children. "
    "If there is no such note, return no notes."
)


def _compact(text: str) -> str:
    return re.sub(r"[^a-z0-9]", "", text.lower())


def on_page(note: str, page: str) -> bool:
    compact = _compact(note)
    return len(compact) >= _MIN_CHARS and compact in _compact(page)


def chapter_at(pdf_page: int, toc: dict) -> str | None:
    for chapter in toc["chapters"]:
        first, last = chapter["pdf_pages"]
        if first <= pdf_page <= last:
            return chapter["chapter_id"]
    return None


def extract(book_id: str, book_title: str, texts: list[str], toc: dict, chat: ChatFn, model: str) -> tuple[dict, list[str]]:
    """The Book guidance document, and the notes rejected because they were not on their page."""
    book_notes, chapter_notes, rejected = [], [], []
    for index, text in enumerate(texts):
        if not HEADING.search(text):
            continue
        page = index + 1
        chapter_id = chapter_at(page, toc)
        for note in chat(PROMPT, text, SCHEMA)["notes"]:
            body = " ".join(note["text"].split())
            if not on_page(body, text):
                rejected.append(f"p{page}: {body[:60]!r}")
                continue
            heading = " ".join(note["heading"].split()) or "For Teachers"
            if chapter_id:
                chapter_notes.append({"chapter_id": chapter_id, "page": page, "heading": heading, "text": body})
            else:
                book_notes.append({"heading": heading, "pages": [page], "text": body})
    document = {
        "book_id": book_id,
        "book_title": book_title,
        "book_notes": book_notes,
        "chapter_notes": chapter_notes,
        "meta": {"model": model, "prompt_version": PROMPT_VERSION, "verified": True},
    }
    return document, rejected


def write(document: dict, target: Path) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(document, indent=2, ensure_ascii=False), encoding="utf-8")
