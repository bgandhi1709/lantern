#!/usr/bin/env python3
"""Write Chapter summaries from a JSON map of chapter id to text (#82, #99).

    .venv/bin/python scripts/write_summaries.py --board ssc --model claude-opus-5-5 < summaries.json

The text is written in a Claude Code session from the refined Chapters, so there is no API spend.
Each summary is checked (20–70 words, plain text); an empty or failing one, or one for a Chapter
that has no refined file, is reported and not written. Existing summaries are kept without --force.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from ncert_build import pipeline, summary
from ncert_build.catalog import book_id_of
from ncert_build.config import Settings


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--board", choices=["cbse", "ssc"], default="cbse")
    parser.add_argument("--model", required=True)
    parser.add_argument("--force", action="store_true")
    args = parser.parse_args()

    settings = Settings.from_env(args.board)
    settings.ensure_outside_repo()
    layout = pipeline.Layout(settings.data_dir)
    failed = 0
    for chapter_id, text in json.load(sys.stdin).items():
        book_id = book_id_of(chapter_id)
        if not layout.refined(book_id, chapter_id).exists():
            outcome = "no refined Chapter, not written"
        else:
            target = settings.data_dir / "summaries" / book_id / f"{chapter_id}.json"
            outcome = summary.write(target, chapter_id, args.board, args.model, text, args.force)
        failed += outcome not in ("written", "exists, kept")
        print(f"{chapter_id}: {outcome}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
