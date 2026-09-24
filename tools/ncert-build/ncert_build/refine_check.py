"""Checks a refined chapter against the refine-v1 contract, locally and for free.

Claude runs this instead of re-reading its own output. The limits are the ones in the
ncert-refine skill; keep the two in step.
"""

from __future__ import annotations

import re

SCHEMA_VERSION = "refine-v1"
CONCEPTS = (2, 8)
QA_PER_CONCEPT = (5, 8)
WORDS = {"summary": 40, "how_taught": 40, "q": 25, "a": 60, "try": 25}


def _words(text: str) -> int:
    return len(text.split())


def check(refined: dict, chapter_id: str, page_count: int) -> list[str]:
    errors: list[str] = []

    def need(condition: bool, message: str) -> None:
        if not condition:
            errors.append(message)

    need(refined.get("schema_version") == SCHEMA_VERSION, f"schema_version must be {SCHEMA_VERSION!r}")
    need(refined.get("chapter_id") == chapter_id, f"chapter_id must be {chapter_id!r}")
    need(bool(str(refined.get("chapter_title", "")).strip()), "chapter_title is empty")
    need(bool(str(refined.get("model", "")).strip()), "model is empty")

    concepts = refined.get("concepts")
    if not isinstance(concepts, list):
        return errors + ["concepts must be a list"]
    need(CONCEPTS[0] <= len(concepts) <= CONCEPTS[1], f"{len(concepts)} concepts; expected {CONCEPTS[0]}–{CONCEPTS[1]}")

    names = [str(c.get("name", "")).strip().lower() for c in concepts]
    need(len(set(names)) == len(names), "concept names must be unique")

    for index, concept in enumerate(concepts, start=1):
        where = f"concept {index}"
        need(concept.get("id") == f"{chapter_id}-c{index}", f"{where}: id must be {chapter_id}-c{index}")
        need(0 < len(str(concept.get("name", ""))) <= 60, f"{where}: name must be 1–60 characters")
        for field in ("summary", "how_taught"):
            text = str(concept.get(field, ""))
            need(bool(text.strip()), f"{where}: {field} is empty")
            need(_words(text) <= WORDS[field], f"{where}: {field} over {WORDS[field]} words")
        need(isinstance(concept.get("key_terms"), list), f"{where}: key_terms must be a list")
        need(isinstance(concept.get("prerequisites"), list), f"{where}: prerequisites must be a list")
        pages = concept.get("pages")
        need(
            isinstance(pages, list) and bool(pages) and all(isinstance(p, int) and 1 <= p <= page_count for p in pages),
            f"{where}: pages must be chapter pages 1–{page_count}",
        )
        qa = concept.get("qa")
        if not isinstance(qa, list):
            errors.append(f"{where}: qa must be a list")
            continue
        need(QA_PER_CONCEPT[0] <= len(qa) <= QA_PER_CONCEPT[1], f"{where}: {len(qa)} qa; expected {QA_PER_CONCEPT[0]}–{QA_PER_CONCEPT[1]}")
        for number, pair in enumerate(qa, start=1):
            for field in ("q", "a"):
                text = str(pair.get(field, ""))
                need(bool(text.strip()), f"{where} qa {number}: {field} is empty")
                need(_words(text) <= WORDS[field], f"{where} qa {number}: {field} over {WORDS[field]} words")
            if "try" in pair:
                need(bool(str(pair["try"] or "").strip()), f"{where} qa {number}: leave out try instead of leaving it empty")
                need(_words(str(pair["try"] or "")) <= WORDS["try"], f"{where} qa {number}: try over {WORDS['try']} words")
            need(not re.search(r"[*_#`]", pair.get("a", "")), f"{where} qa {number}: no markdown in answers")
    return errors


def summary(refined: dict) -> str:
    concepts = refined.get("concepts", [])
    return f"{len(concepts)} concepts, {sum(len(c.get('qa', [])) for c in concepts)} Q&A"
