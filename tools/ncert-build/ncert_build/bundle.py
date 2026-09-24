"""Stage 6: one compact Markdown file per chapter, the only thing Claude reads when refining.

It holds the local draft's concepts as hints, then the cleaned chapter text with page markers.
Nothing else: no JSON punctuation, no PDF, no metadata Claude doesn't need. That keeps each
refinement to one small read (Class 1 chapters are roughly 1–2k tokens).
"""

from __future__ import annotations

from pathlib import Path

MAX_HINT_QUESTIONS = 3


def stage(grade: int) -> str:
    """NCF-2023 stages, used for the answer cache key (D15)."""
    if grade <= 2:
        return "foundational"
    if grade <= 5:
        return "preparatory"
    if grade <= 8:
        return "middle"
    return "secondary"


def render(chapter: dict, draft: dict | None, output_path: str) -> str:
    pages = sorted({n for s in chapter["sections"] for n in s["pages"]})
    lines = [
        f"# {chapter['chapter_id']} · Class {chapter['grade']} {chapter['subject']} · {chapter['book_title']}",
        f"stage: {stage(chapter['grade'])} · pages: {pages[0]}–{pages[-1]}" if pages else "",
        f"write to: {output_path}",
        "",
        "## Draft concepts (local model: hints only, may be wrong or split too finely)",
    ]
    if draft:
        if draft.get("chapter_title"):
            lines.append(f"title guess: {draft['chapter_title']}")
        for number, concept in enumerate(draft["concepts"], start=1):
            pages_text = ",".join(str(p) for p in concept.get("pages", []))
            questions = " / ".join(concept.get("kid_questions", [])[:MAX_HINT_QUESTIONS])
            lines.append(f"{number}. {concept['name']} [p{pages_text}]: {concept['summary']}")
            if questions:
                lines.append(f"   q: {questions}")
    else:
        lines.append("(no draft)")

    lines += ["", "## Chapter text"]
    for section in chapter["sections"]:
        marker = f"[p{section['pages'][0]}]" if section["pages"] else ""
        heading = f" {section['heading']}" if section.get("heading") else ""
        lines.append(f"{marker}{heading}")
        lines.append(section["text"].strip())
    return "\n".join(lines).strip() + "\n"


def write(text: str, target: Path) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(text, encoding="utf-8")
