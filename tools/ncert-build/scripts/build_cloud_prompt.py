#!/usr/bin/env python3
"""Split one book's refine task into cloud-session-sized message chunks.

A single CLI argument to `claude --cloud` hits the Linux MAX_ARG_STRLEN limit at 128KiB
(exec fails with "Argument list too long" past that, well below the 2MiB ARG_MAX for the
whole argv) - see docs/ideation/decision-log.md, cloud-session cost pilot. Most books
exceed that in one chapter's text alone once repeated across many chapters, so a book's
task is sent as several messages to the same cloud session instead of one.

Usage:
    build_cloud_prompt.py <book-id> <out-dir>

Writes <out-dir>/00-open.txt (the --cloud opening message: schema + rules + as many
leading chapters as fit) and <out-dir>/NN-continue.txt for each remaining chunk of
chapters, plus <out-dir>/99-finish.txt (the commit-and-push instruction). Each file stays
under the safe per-message budget (default 100000 bytes, comfortably under the 131072
hard limit) so every message can be sent as a single argument or via stdin.
"""
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
DATA_DIR = REPO_ROOT.parent / "lantern-data"
SAFE_MESSAGE_BYTES = 100_000

INSTRUCTIONS = """\
Refine NCERT chapters into concept cards and parent Q&A (schema refine-v1). This runs as
a Claude Code cloud session for book {book} ({n} chapters total) - there is no local data
directory here; every chapter's full text is inlined in this and the messages that
follow in this same conversation. Work through all {n} chapters across this whole
conversation, not just the ones in this first message - more will follow as separate
messages, in order, before you're asked to commit and push.

## Output schema (refine-v1), one such JSON object per chapter

{{
  "schema_version": "refine-v1",
  "chapter_id": "<id>",
  "chapter_title": "<title>",
  "model": "<your model id>",
  "concepts": [
    {{
      "id": "<id>-c1",
      "name": "<concept name>",
      "summary": "<at most 40 words, for the mother>",
      "how_taught": "<the book's own method, at most 40 words>",
      "key_terms": ["..."],
      "prerequisites": ["<plain names of concepts the child needed before this chapter>"],
      "pages": [1, 2],
      "qa": [
        {{"q": "<how a child of this class would ask it>", "a": "<what the mother can say, at most 60 words, plain text>", "try": "<one-line home activity, omit key if none fits>"}}
      ]
    }}
  ]
}}

## Rules (same for every chapter)
- Source: facts come only from each chapter's own text and picture descriptions. The
  draft concepts section is a hint list and can be wrong or split too finely - the
  chapter text wins where they disagree.
- Concepts: 2-8, one per idea. Merge draft items that are the same idea taught through
  different activities. Ids run <chapter_id>-c1, -c2, ... in order.
- pages: chapter page numbers from the [pN] markers, picture blocks included.
- how_taught: the book's own method, so the mother teaches it the same way.
- prerequisites: what the child learned before this chapter, as plain names. Empty list
  for Class 1, or when the chapter shows nothing. Use only earlier chapters of this same
  book that you have already refined earlier in this conversation - never a different
  book, and never a chapter you have not seen the text of yet.
- qa: 5-8 per concept. q = how the child would ask it. a = at most 60 words, plain text.
  try = one-line home activity, leave the key out otherwise.
- summary: at most 40 words.

Write each chapter's JSON to `cloud-output/{book}/<chapter_id>.json` in this repo
working directory as you finish it (create directories as needed). Do not commit yet -
that happens in a later message. Do not modify any other file. Do not run the
ncert-build tool or its checker - that runs locally after this session's output is
fetched. After each chapter, reply with one line: "<chapter_id>: N concepts, M Q&A".

## Chapters in this message

"""

CONTINUE_HEADER = "## More chapters of {book} (continuing this session, {n} total in this book)\n\n"

FINISH_MESSAGE = """\
That was the last chapter of {book}. Commit everything under `cloud-output/{book}/` in
one commit, message "Cloud refine: {book} ({n} chapters)", and push it to a new branch
named `cloud-refine-{book}`. Reply with just the branch name once pushed.
"""


def chapter_block(index: int, total: int, path: Path) -> str:
    return f"\n### Chapter {index}/{total}: {path.stem}\n\n{path.read_text(encoding='utf-8')}\n"


def main() -> None:
    if len(sys.argv) != 3:
        print(f"usage: {sys.argv[0]} <book-id> <out-dir>", file=sys.stderr)
        raise SystemExit(2)
    book, out_dir = sys.argv[1], Path(sys.argv[2])
    all_chapters = sorted((DATA_DIR / "bundles" / book).glob("*.md"))
    if not all_chapters:
        print(f"no bundles found for {book}", file=sys.stderr)
        raise SystemExit(1)
    refined_dir = DATA_DIR / "refined" / book
    chapter_files = [p for p in all_chapters if not (refined_dir / f"{p.stem}.json").exists()]
    if not chapter_files:
        print(f"{book}: all {len(all_chapters)} chapters already refined, nothing to do")
        raise SystemExit(0)
    out_dir.mkdir(parents=True, exist_ok=True)

    n = len(chapter_files)
    chunks: list[str] = []
    current = INSTRUCTIONS.format(book=book, n=n)
    is_first = True
    for i, path in enumerate(chapter_files, 1):
        block = chapter_block(i, n, path)
        if len(current) + len(block) > SAFE_MESSAGE_BYTES and current.strip():
            chunks.append(current)
            current = "" if not is_first else ""
            current = CONTINUE_HEADER.format(book=book, n=n)
            is_first = False
        current += block
    chunks.append(current)

    (out_dir / "00-open.txt").write_text(chunks[0], encoding="utf-8")
    for idx, chunk in enumerate(chunks[1:], 1):
        (out_dir / f"{idx:02d}-continue.txt").write_text(chunk, encoding="utf-8")
    (out_dir / "99-finish.txt").write_text(FINISH_MESSAGE.format(book=book, n=n), encoding="utf-8")

    print(f"{book}: {n} chapters -> {len(chunks)} opening/continue message(s) + 1 finish, in {out_dir}")


if __name__ == "__main__":
    main()
