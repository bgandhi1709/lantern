"""Super context (#41): graded, code-checked cross-chapter context for refine bundles.

No LLM here. This builds the candidate lists a closed-question stage will later hand to the 8B
(step 3), and checks its quoted picks the same way `refine_check` checks Claude's own output:
in code, exact match only. D17: earlier concepts come from the same book only, never another
book. D19: Python checks that a quote is real; it can't check that the pick itself is right, and
that gap is accepted, not solved here.
"""

from __future__ import annotations

import re

from .draft import ChatFn

SIZE_CAP_FRACTION = 0.25  # of the current chapter's own token estimate
NUM_CTX = 8192  # same as draft.py: a chapter passage plus a short candidate list fits easily

_WHITESPACE = re.compile(r"\s+")

QUESTION_SCHEMA = {
    "type": "object",
    "properties": {
        "picks": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "earlier_concept_index": {"type": "integer"},
                    "quote": {"type": "string"},
                },
                "required": ["earlier_concept_index", "quote"],
            },
        },
    },
    "required": ["picks"],
}

QUESTION_SYSTEM = """You read one passage of a Class {grade} NCERT textbook chapter. Below it is a
numbered list of concepts already taught earlier in this same book. For each earlier concept the
CURRENT passage actually uses, builds on or refers back to, give its list number and quote the
exact line from the CURRENT passage that shows it. Quote only from the passage given here, word
for word: do not paraphrase, shorten or invent a line. Most earlier concepts are usually not used
by a given passage — only pick one you can quote. Give an empty list if none apply."""


def token_estimate(text: str) -> int:
    """chars // 4: the same rough English-token proxy draft.py's chunking uses."""
    return len(text) // 4


def normalize_whitespace(text: str) -> str:
    return _WHITESPACE.sub(" ", text).strip()


def quote_found(quote: str, chapter_text: str) -> bool:
    """Whether `quote` appears verbatim in `chapter_text`.

    Exact match, whitespace-normalised only — no fuzzy matching. An empty or blank quote is
    never found: a pick with nothing to check behind it earns no credit.
    """
    if not quote.strip():
        return False
    return normalize_whitespace(quote) in normalize_whitespace(chapter_text)


def earlier_concepts(chapter_ids: list[str], refined_by_chapter: dict[str, dict]) -> list[dict]:
    """Concepts from `chapter_ids`, in that order, read out of each chapter's refine-v1 document.

    `chapter_ids` is this book's own chapters before the one being built (D17: same book only,
    caller decides the cutoff). A chapter with no entry in `refined_by_chapter` — not refined
    yet — is skipped, not an error. Each returned concept keeps only what a candidate list needs:
    chapter_id, id, name, summary, key_terms.
    """
    concepts = []
    for chapter_id in chapter_ids:
        document = refined_by_chapter.get(chapter_id)
        if document is None:
            continue
        for concept in document.get("concepts", []):
            concepts.append(
                {
                    "chapter_id": chapter_id,
                    "id": concept["id"],
                    "name": concept["name"],
                    "summary": concept["summary"],
                    "key_terms": list(concept.get("key_terms", [])),
                }
            )
    return concepts


def first_appearance_index(concepts: list[dict]) -> dict[str, dict]:
    """term (lowercased, trimmed) -> the first concept in `concepts` order that teaches it.

    `concepts` order decides "first": pass `earlier_concepts()`'s chapter-order output to mean
    "first taught in the book so far". A later repeat of the same term is ignored.
    """
    index: dict[str, dict] = {}
    for concept in concepts:
        for term in concept.get("key_terms", []):
            key = term.strip().lower()
            if key and key not in index:
                index[key] = concept
    return index


def cap_to_budget(concepts: list[dict], chapter_text: str, fraction: float = SIZE_CAP_FRACTION) -> list[dict]:
    """Keeps concepts, in the given order, until the next one would push the running token
    estimate (summary + key_terms — the only fields a candidate list carries) past `fraction` of
    the current chapter's own token estimate. Order is the caller's call: pass concepts nearest
    chapter first for a recency-biased cap. Always keeps at least the first concept, even if it
    alone is over budget, so the cap never produces an empty list from a non-empty input.
    """
    budget = int(token_estimate(chapter_text) * fraction)
    kept: list[dict] = []
    used = 0
    for concept in concepts:
        cost = token_estimate(concept["summary"]) + token_estimate(" ".join(concept.get("key_terms", [])))
        if kept and used + cost > budget:
            break
        kept.append(concept)
        used += cost
    return kept


def ask_which_earlier_concepts(chapter_text: str, candidates: list[dict], chat: ChatFn, grade: int) -> list[dict]:
    """The closed 8B question: which of `candidates` (from `earlier_concepts`, in the order
    given) does `chapter_text` use, quoting the line that shows it.

    The model picks only from the numbered list handed to it and must quote a real line back
    (the closed-8B rule, #41): Python then checks every quote against `chapter_text` with
    `quote_found` and drops any pick that fails, an out-of-range index, or a repeat index. This
    checks that a quote is real, never that the pick itself is the right one (D19 — a known,
    accepted gap). Returns each kept candidate with its checked `quote` added.
    """
    if not candidates:
        return []
    listing = "\n".join(f"{i}. {c['name']}: {c['summary']}" for i, c in enumerate(candidates))
    user = f"Earlier concepts:\n{listing}\n\nCurrent passage:\n{chapter_text}"
    reply = chat(QUESTION_SYSTEM.format(grade=grade), user, QUESTION_SCHEMA)

    checked = []
    seen_indices: set[int] = set()
    for pick in reply.get("picks", []):
        index = pick.get("earlier_concept_index")
        quote = str(pick.get("quote", ""))
        if not isinstance(index, int) or index in seen_indices or not (0 <= index < len(candidates)):
            continue
        if not quote_found(quote, chapter_text):
            continue
        seen_indices.add(index)
        checked.append({**candidates[index], "quote": quote})
    return checked
