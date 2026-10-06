"""Chapter summaries (summary-v1): a few plain lines for the Parent on what a Chapter is about,
shown before Plan. Written once, offline, from the refined Chapter; nothing here calls a model.
"""

from __future__ import annotations

import json
import re
from pathlib import Path

SCHEMA_VERSION = "summary-v1"
WORDS = (20, 70)
_MARKDOWN = re.compile(r"[*_#`]")


def problems(text: str) -> list[str]:
    words = len(text.split())
    found = []
    if not WORDS[0] <= words <= WORDS[1]:
        found.append(f"{words} words; expected {WORDS[0]}–{WORDS[1]}")
    if _MARKDOWN.search(text):
        found.append("markdown")
    return found


def write(target: Path, chapter_id: str, board: str, model: str, text: str, force: bool = False) -> str:
    """Writes one summary and says what happened; an empty or invalid one is never written, and an
    existing one is kept unless ``force``."""
    text = " ".join(text.split())
    if not text:
        return "empty, not written"
    if found := problems(text):
        return f"not written: {', '.join(found)}"
    if target.exists() and not force:
        return "exists, kept"
    document = {"schema_version": SCHEMA_VERSION, "board": board, "chapter_id": chapter_id, "model": model, "summary": text}
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(document, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return "written"
