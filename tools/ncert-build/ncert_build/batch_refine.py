"""Refine through the Batch API: one request per Chapter, the refine-v1 rules as the system prompt
(too short to cache), the Chapter (its text, picture readings and, for an English Unit, its
lessons) as the user turn, and a JSON schema so every reply parses. Ids, model and versions are
filled in here, not by the model, so they cost no output tokens and cannot be wrong.
"""

from __future__ import annotations

import json
import re
from pathlib import Path

from .refine_check import SCHEMA_VERSION

MAX_TOKENS = 32000  # thinking counts against it; a small cap returns empty text

SYSTEM = """You turn one chapter of a Maharashtra State Board (Balbharati) school book into concept \
cards and Q&A for Lantern, an app that helps a parent teach their own young child at home. The \
parent reads basic English and finds explaining concepts hard.

Facts come only from the chapter text and its picture readings ([fig …] lines, written by a local \
model). Use the book's own examples, names, numbers and objects; never invent new ones. Text such \
as "For Teachers : …" is the publisher's advice to the adult: use it to shape how_taught, never as \
a question for the child, and never mention a teacher in an answer — speak to the parent.

Concepts: 2–8, one per idea the chapter teaches, in book order; never more than 8. An English \
Unit may have up to 12, and each names its lesson by number only (lesson: "1.3"). Merge \
activities that teach the same idea. Every limit below is a hard maximum: count the words.
- name: at most 60 characters.
- summary: at most 40 words, for the parent.
- how_taught: the book's own method, so the parent teaches it the same way. At most 40 words.
- key_terms: the words the child should learn.
- prerequisites: what the child learned before, as plain names; empty for Class 1 or when the \
chapter shows nothing.
- pages: chapter page numbers from the [pN] markers.
- qa: 5–8 per concept. q: how a child of this class would ask it, at most 25 words. a: what the \
parent can say to the child, in words that child knows, short sentences, plain text, no markdown, \
at most 60 words. try: a one-line activity with things at home, at most 25 words, when one fits; \
leave it out otherwise."""

_ENTRY = {"type": "string"}
_PAGES = {"type": "array", "items": {"type": "integer"}}
SCHEMA = {
    "type": "object",
    "additionalProperties": False,
    "required": ["chapter_title", "concepts"],
    "properties": {
        "chapter_title": _ENTRY,
        "concepts": {
            "type": "array",
            "items": {
                "type": "object",
                "additionalProperties": False,
                "required": ["name", "summary", "how_taught", "key_terms", "prerequisites", "pages", "qa"],
                "properties": {
                    "lesson": _ENTRY,
                    "name": _ENTRY,
                    "summary": _ENTRY,
                    "how_taught": _ENTRY,
                    "key_terms": {"type": "array", "items": _ENTRY},
                    "prerequisites": {"type": "array", "items": _ENTRY},
                    "pages": _PAGES,
                    "qa": {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "additionalProperties": False,
                            "required": ["q", "a"],
                            "properties": {"q": _ENTRY, "a": _ENTRY, "try": _ENTRY},
                        },
                    },
                },
            },
        },
    },
}


def chapter_input(bundle: str, lessons: list[dict]) -> str:
    """The bundle without its write-to line and the local model's draft (the model groups ideas
    better on its own, and the draft is 13% of the input), plus an English Unit's lessons."""
    bundle = re.sub(r"(?m)^write to: .*\n", "", bundle)
    bundle = re.sub(r"(?s)## Draft concepts.*?(?=## Chapter text)", "", bundle)
    if lessons:
        listed = "\n".join(f"{l['number']} {l['title']} (book page {l['printed_page']})" for l in lessons)
        bundle = f"This chapter is an English Unit. Its lessons:\n{listed}\n\n{bundle}"
    return bundle


def request(custom_id: str, model: str, effort: str, text: str) -> dict:
    return {
        "custom_id": custom_id,
        "params": {
            "model": model,
            "max_tokens": MAX_TOKENS,
            "system": SYSTEM,
            "messages": [{"role": "user", "content": text}],
            "output_config": {"effort": effort, "format": {"type": "json_schema", "schema": SCHEMA}},
        },
    }


def assemble(reply: dict, chapter_id: str, model: str, lessons: list[dict]) -> dict:
    """The refine-v1 document from the model's reply; ids and versions are ours."""
    refined = {
        "schema_version": SCHEMA_VERSION,
        "board": "ssc",
        "chapter_id": chapter_id,
        "chapter_title": reply["chapter_title"],
        "model": model,
    }
    if lessons:
        refined["lessons"] = [{"number": l["number"], "title": l["title"]} for l in lessons]
    refined["concepts"] = [
        {"id": f"{chapter_id}-c{n}", **_lesson_number(concept)} for n, concept in enumerate(reply["concepts"], start=1)
    ]
    return refined


def _lesson_number(concept: dict) -> dict:
    """The model echoes the lesson as listed ("1.1 Holidays are Over"); keep the number."""
    match = re.match(r"\s*(\d+(?:\.\d+)?)", str(concept.get("lesson", "")))
    return {**concept, "lesson": match.group(1)} if match else concept


def unit_lessons(toc_path: Path, chapter_id: str) -> list[dict]:
    toc = json.loads(toc_path.read_text(encoding="utf-8"))
    return next((c["lessons"] for c in toc["chapters"] if c["chapter_id"] == chapter_id), [])
