"""Stage 6v2 (#41): bundle v2 — bundle.py's layout, plus a graded "Super context" section.

A new module. bundle.py, its output and ../lantern-data/bundles/ are untouched (hard constraint):
this only reads bundle.render()'s v1 markdown and splices in one more section. Bundle v2 goes to
../lantern-data/bundles-v2/ only. The chapter text itself is never summarised or compressed — the
new section is additive and placed before it, so "the text wins" (bundle.py's own rule) still
holds: the chapter text after it is exactly what bundle.render() would have written alone.
"""

from __future__ import annotations

from pathlib import Path

from .bundle import render as render_v1

CHAPTER_TEXT_HEADING = "## Chapter text"
SUPER_CONTEXT_HEADING = "## Super context (earlier concepts this chapter's text uses)"


def _context_lines(earlier_context: list[dict]) -> list[str]:
    """One line per kept earlier concept: which chapter taught it, what it is, and the exact
    quote from the CURRENT chapter's own text that shows it in use (from
    super_context.ask_which_earlier_concepts — already quote-checked, never taken on trust).
    """
    if not earlier_context:
        return [SUPER_CONTEXT_HEADING, "(none found)"]
    lines = [SUPER_CONTEXT_HEADING]
    for concept in earlier_context:
        lines.append(f"- {concept['chapter_id']} · {concept['name']}: {concept['summary']}")
        lines.append(f"  used here: \"{concept['quote']}\"")
    return lines


def render(
    chapter: dict,
    draft: dict | None,
    output_path: str,
    earlier_context: list[dict],
    pictures: list[dict] | None = None,
) -> str:
    """bundle.render()'s v1 text, with "Super context" inserted between "Draft concepts" and
    "Chapter text". Everything from "Chapter text" onward is byte-for-byte what v1 would render.
    """
    v1 = render_v1(chapter, draft, output_path, pictures)
    before, heading, after = v1.partition(CHAPTER_TEXT_HEADING)
    if not heading:
        raise ValueError("bundle.render() output has no 'Chapter text' section to splice before")
    section = "\n".join(_context_lines(earlier_context))
    return f"{before}{section}\n\n{heading}{after}"


def write(text: str, target: Path) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(text, encoding="utf-8")
