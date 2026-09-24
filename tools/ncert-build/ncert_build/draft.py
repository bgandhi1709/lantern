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

PROMPT_VERSION = "draft-v2"
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
List the concepts the passage teaches. A concept is one idea a child learns, for example
"Positional words (above, below, inside, outside)" or "Prime factorisation". Poems, stories,
activities and examples that teach the same idea belong to that ONE concept: never make a
separate concept per activity, example or exercise. A passage usually teaches 2 to 6 concepts.
For each concept give:
- name: a short, specific name
- summary: one or two sentences, using only what the passage says
- key_terms: words a child must know for it
- pages: the page numbers (from the [page N] markers) where it is taught
- kid_questions: 3 to 5 questions a Class {grade} child might ask a parent about the idea itself,
  in simple English, phrased the way a child talks (not questions about the book's pictures)
Rules: use only the passage. Do not add facts that are not in it. Skip teacher instructions,
answers to exercises and decorative text. chapter_title: the chapter's title as printed at its
start, not a section heading; an empty string if the passage does not show it."""

CONSOLIDATE_SCHEMA = {
    "type": "object",
    "properties": {
        "concepts": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "name": {"type": "string"},
                    "summary": {"type": "string"},
                    "members": {"type": "array", "items": {"type": "integer"}},
                },
                "required": ["name", "summary", "members"],
            },
        },
    },
    "required": ["concepts"],
}

CONSOLIDATE_SYSTEM = """You tidy the concept list of one chapter of a Class {grade} NCERT textbook.
The list was drafted passage by passage, so the same idea often appears more than once under
different names, or once per activity. Group the numbered concepts so that each group is ONE idea
a child learns. Give each group a short, specific name and a one- or two-sentence summary built
only from the summaries given. members: the numbers of the concepts in the group. Every number
must appear in exactly one group. Keep genuinely different ideas in separate groups."""

MIN_CONCEPTS_TO_CONSOLIDATE = 4


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


ChatFn = Callable[[str, str, dict], dict]


def consolidate(concepts: list[dict], chat: ChatFn, grade: int) -> list[dict]:
    """Second pass: the model groups near-duplicate concepts; the grouping is applied here.

    The model only returns names, summaries and member numbers, so terms, pages and questions
    can't be lost or invented. A concept the model leaves out of every group is kept as it was.
    """
    if len(concepts) < MIN_CONCEPTS_TO_CONSOLIDATE:
        return concepts
    listing = "\n".join(f"{i}. {c['name']}: {c['summary']}" for i, c in enumerate(concepts))
    reply = chat(CONSOLIDATE_SYSTEM.format(grade=grade), f"Concepts:\n{listing}", CONSOLIDATE_SCHEMA)

    used: set[int] = set()
    grouped = []
    for group in reply.get("concepts", []):
        members = [m for m in group.get("members", []) if 0 <= m < len(concepts) and m not in used]
        if not members:
            continue
        used.update(members)
        parts = [concepts[m] for m in members]
        grouped.append(
            {
                "name": group.get("name", "").strip() or parts[0]["name"],
                "summary": group.get("summary", "").strip() or parts[0]["summary"],
                "key_terms": _unique([t for p in parts for t in p["key_terms"]]),
                "pages": sorted({n for p in parts for n in p["pages"]}),
                "kid_questions": _unique([q for p in parts for q in p["kid_questions"]]),
                "merged_from": [p["name"] for p in parts] if len(parts) > 1 else [],
            }
        )
    grouped.extend(c for i, c in enumerate(concepts) if i not in used)
    return grouped


def draft_chapter(chapter: dict, chat: ChatFn, model: str) -> dict:
    system = SYSTEM.format(grade=chapter["grade"])
    header = f"Class {chapter['grade']} {chapter['subject']}, book \"{chapter['book_title']}\"."
    replies = [chat(system, f"{header}\n\nPassage:\n{text}", SCHEMA) for text in chunks(chapter)]
    result = merge(replies)
    drafted = len(result["concepts"])
    result["concepts"] = consolidate(result["concepts"], chat, chapter["grade"])
    return {
        "chapter_id": chapter["chapter_id"],
        "status": "draft",
        "chapter_title": result["chapter_title"] or chapter.get("title_guess") or "",
        "concepts": result["concepts"],
        "meta": {
            "model": model,
            "prompt_version": PROMPT_VERSION,
            "thinking": False,
            "chunks": len(replies),
            "concepts_before_consolidation": drafted,
            "source_sha256": chapter.get("source_sha256"),
            "created_at": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        },
    }


def write(draft: dict, target: Path) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(draft, indent=2, ensure_ascii=False), encoding="utf-8")
