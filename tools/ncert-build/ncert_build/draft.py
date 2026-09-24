"""Stage 5: a local model drafts the concept list and kid-style questions for each chapter.

This is a draft, never the source of truth. Claude refines it later against the chapter text
itself (#34), so a mistake here costs refinement effort but can't reach a mother. The prompt is
versioned with every file so drafts made with different prompts can be told apart.
"""

from __future__ import annotations

import json
import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Callable

PROMPT_VERSION = "draft-v1"
MAX_CHUNK_CHARS = 12_000  # ~3k tokens of English, leaving room for the reply in an 8k context
NUM_CTX = 8192

SCHEMA = {
    "type": "object",
    "properties": {
        "chapter_title": {"type": "string"},
        "concepts": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "name": {"type": "string"},
                    "summary": {"type": "string"},
                    "key_terms": {"type": "array", "items": {"type": "string"}},
                    "pages": {"type": "array", "items": {"type": "integer"}},
                    "kid_questions": {"type": "array", "items": {"type": "string"}},
                },
                "required": ["name", "summary", "key_terms", "pages", "kid_questions"],
            },
        },
    },
    "required": ["chapter_title", "concepts"],
}

SYSTEM = """You analyse one passage of an Indian NCERT school textbook for a parent-teacher app.
List the concepts the passage teaches. For each concept give:
- name: a short, specific name (e.g. "Prime factorisation", not "Numbers")
- summary: one or two sentences, using only what the passage says
- key_terms: words a child must know for it
- pages: the page numbers (from the [page N] markers) where it is taught
- kid_questions: 3 to 5 questions a Class {grade} child might ask a parent about it, in simple
  English, phrased the way a child talks
Rules: use only the passage. Do not add facts that are not in it. Skip teacher instructions,
exercises' answers and decorative text. chapter_title: the chapter's title if the passage shows it,
otherwise an empty string."""


def chunks(chapter: dict, max_chars: int = MAX_CHUNK_CHARS) -> list[str]:
    """Section texts with page markers, packed into chunks that fit the model's context."""
    pieces = []
    for section in chapter["sections"]:
        marker = f"[page {section['pages'][0]}]" if section["pages"] else ""
        heading = f"## {section['heading']}\n" if section.get("heading") else ""
        pieces.append(f"{marker}\n{heading}{section['text']}".strip())

    packed, current = [], ""
    for piece in pieces:
        while len(piece) > max_chars:  # one oversized section: split it on its own
            packed.append(piece[:max_chars])
            piece = piece[max_chars:]
        if current and len(current) + len(piece) + 2 > max_chars:
            packed.append(current)
            current = ""
        current = f"{current}\n\n{piece}" if current else piece
    if current:
        packed.append(current)
    return packed


def _key(name: str) -> str:
    return re.sub(r"[^a-z0-9]+", " ", name.lower()).strip()


def _unique(items: list) -> list:
    seen, out = set(), []
    for item in items:
        marker = item.lower().strip() if isinstance(item, str) else item
        if marker not in seen:
            seen.add(marker)
            out.append(item)
    return out


def merge(replies: list[dict]) -> dict:
    """Combines per-chunk replies: concepts with the same name are merged, not repeated."""
    title = next((r.get("chapter_title", "").strip() for r in replies if r.get("chapter_title", "").strip()), "")
    merged: dict[str, dict] = {}
    for reply in replies:
        for concept in reply.get("concepts", []):
            name = concept.get("name", "").strip()
            if not name:
                continue
            existing = merged.get(_key(name))
            if existing is None:
                merged[_key(name)] = {
                    "name": name,
                    "summary": concept.get("summary", "").strip(),
                    "key_terms": _unique(concept.get("key_terms", [])),
                    "pages": sorted(set(concept.get("pages", []))),
                    "kid_questions": _unique(concept.get("kid_questions", [])),
                }
            else:
                existing["key_terms"] = _unique(existing["key_terms"] + concept.get("key_terms", []))
                existing["pages"] = sorted(set(existing["pages"]) | set(concept.get("pages", [])))
                existing["kid_questions"] = _unique(existing["kid_questions"] + concept.get("kid_questions", []))
    return {"chapter_title": title, "concepts": list(merged.values())}


ChatFn = Callable[[str, str], dict]


def draft_chapter(chapter: dict, chat: ChatFn, model: str) -> dict:
    system = SYSTEM.format(grade=chapter["grade"])
    header = f"Class {chapter['grade']} {chapter['subject']}, book \"{chapter['book_title']}\"."
    replies = [chat(system, f"{header}\n\nPassage:\n{text}") for text in chunks(chapter)]
    result = merge(replies)
    return {
        "chapter_id": chapter["chapter_id"],
        "status": "draft",
        "chapter_title": result["chapter_title"] or chapter.get("title_guess") or "",
        "concepts": result["concepts"],
        "meta": {
            "model": model,
            "prompt_version": PROMPT_VERSION,
            "chunks": len(replies),
            "source_sha256": chapter.get("source_sha256"),
            "created_at": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        },
    }


def write(draft: dict, target: Path) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(draft, indent=2, ensure_ascii=False), encoding="utf-8")
