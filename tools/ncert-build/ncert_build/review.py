"""How good the picture readings are, without spending Claude tokens on images.

Two measures, written under review/ in the data directory:

- report.txt, the GPU's own agreement: per class and figure kind, how many counts an independent
  pixel check or a majority of re-reads confirmed, and how many stayed unsure. Agreement is not
  accuracy, but a count that two methods reach separately is rarely wrong.
- sheet.html, the true measure: a sample of figures with the readings beside each, for a person
  to mark right or wrong in a browser. The marks are saved as marks.json (download it into
  review/); the next `review` scores them per class and kind against the 90% target.
"""

from __future__ import annotations

import html
import json
import os
import random
from collections import defaultdict
from pathlib import Path

from .catalog import Book

TARGET = 0.90
SAMPLE_PER_CLASS = 24  # figures on the sheet per class: mostly counted ones


def _figures(layout, books: list[Book]):
    from .pipeline import only_chapters

    for book in books:
        for chapter_id in book.chapter_ids():
            if only_chapters is not None and chapter_id not in only_chapters:
                continue
            path = layout.pictures(book.book_id, chapter_id)
            if path.exists():
                for figure in json.loads(path.read_text(encoding="utf-8")).get("figures", []):
                    yield book, chapter_id, figure


def _numbers(figure: dict) -> list[tuple[str, str, bool]]:
    """(key, what, sure) for each number a figure's reading asserts."""
    out = []
    for i, group in enumerate(figure.get("counts", [])):
        out.append((f"c{i}", f"{group['object']}: {group['count']}", group["sure"]))
    grid = figure.get("grid")
    if grid:
        what = f"grid {grid['rows']}×{grid['columns']}, {grid['coloured']} coloured, {grid['hatched']} hatched"
        out.append(("grid", what, grid["sure"]))
    return out


def report(layout, books: list[Book]) -> str:
    table: dict[tuple[int, str], list[int]] = defaultdict(lambda: [0, 0, 0])  # figures, numbers, sure
    for book, _, figure in _figures(layout, books):
        row = table[(book.grade, figure["kind"])]
        row[0] += 1
        for _, _, sure in _numbers(figure):
            row[1] += 1
            row[2] += sure
    lines = [f"{'class':>5}  {'kind':14} {'figures':>7} {'numbers':>7} {'agreed':>7}"]
    by_class: dict[int, list[int]] = defaultdict(lambda: [0, 0])
    for (grade, kind), (figures, numbers, sure) in sorted(table.items()):
        share = f"{100 * sure // numbers}%" if numbers else "-"
        lines.append(f"{grade:>5}  {kind:14} {figures:>7} {numbers:>7} {share:>7}")
        by_class[grade][0] += numbers
        by_class[grade][1] += sure
    lines.append("")
    for grade, (numbers, sure) in sorted(by_class.items()):
        if numbers:
            lines.append(f"Class {grade}: {sure}/{numbers} numbers agreed ({100 * sure // numbers}%), "
                         f"{numbers - sure} left unsure for Claude")
    return "\n".join(lines)


def score(layout, books: list[Book], marks: dict) -> str:
    """Accuracy of the person's marks: of the numbers marked, how many were right, per class and
    kind, for the ones the GPU was sure of (these reach Claude as fact) and overall."""
    table: dict[tuple[int, str], list[int]] = defaultdict(lambda: [0, 0, 0, 0])  # sure: n, ok; all: n, ok
    kinds = defaultdict(lambda: [0, 0])
    for book, chapter_id, figure in _figures(layout, books):
        key = f"{chapter_id}/p{figure['page']}.{figure['figure']}"
        marked = marks.get(key)
        if not marked:
            continue
        if "kind" in marked:
            kinds[book.grade][0] += 1
            kinds[book.grade][1] += marked["kind"] == "ok"
        row = table[(book.grade, figure["kind"])]
        for number_key, _, sure in _numbers(figure):
            verdict = marked.get(number_key)
            if verdict not in ("ok", "wrong"):
                continue
            row[2] += 1
            row[3] += verdict == "ok"
            if sure:
                row[0] += 1
                row[1] += verdict == "ok"
    lines = [f"{'class':>5}  {'kind':14} {'sure right':>12} {'all right':>11}"]
    for (grade, kind), (sure_n, sure_ok, all_n, all_ok) in sorted(table.items()):
        def pct(ok, n):
            return f"{ok}/{n} {100 * ok // n}%" if n else "-"

        flag = "" if not sure_n or sure_ok / sure_n >= TARGET else "  < 90%"
        lines.append(f"{grade:>5}  {kind:14} {pct(sure_ok, sure_n):>12} {pct(all_ok, all_n):>11}{flag}")
    for grade, (n, ok) in sorted(kinds.items()):
        lines.append(f"Class {grade}: figure kind right {ok}/{n}")
    return "\n".join(lines)


def sheet(layout, books: list[Book], out_dir: Path, name: str = "sheet") -> Path:
    by_class: dict[int, list] = defaultdict(list)
    for book, chapter_id, figure in _figures(layout, books):
        by_class[book.grade].append((book, chapter_id, figure))
    chosen = []
    pick = random.Random(7)
    for grade, items in sorted(by_class.items()):
        counted = [i for i in items if _numbers(i[2])]
        other = [i for i in items if not _numbers(i[2])]
        pick.shuffle(counted)
        pick.shuffle(other)
        take = counted[: SAMPLE_PER_CLASS * 3 // 4]
        chosen += take + other[: SAMPLE_PER_CLASS - len(take)]
    cards = []
    for book, chapter_id, figure in chosen:
        key = f"{chapter_id}/p{figure['page']}.{figure['figure']}"
        source = os.path.relpath(figure["image"], out_dir)
        rows = [("kind", f"kind: {figure['kind']} — {figure['summary']}")]
        if figure.get("labels"):
            rows.append(("labels", "labels: " + " · ".join(figure["labels"])))
        rows += [(k, what + ("" if sure else "  (GPU unsure)")) for k, what, sure in _numbers(figure)]
        checks = "".join(
            f'<div class="row"><span>{html.escape(text)}</span>'
            f'<label><input type="radio" name="{html.escape(key)}|{k}" value="ok">right</label>'
            f'<label><input type="radio" name="{html.escape(key)}|{k}" value="wrong">wrong</label></div>'
            for k, text in rows
        )
        cards.append(
            f'<section><h2>Class {book.grade} · {html.escape(key)}</h2>'
            f'<img src="{html.escape(source)}" loading="lazy">{checks}</section>'
        )
    page = SHEET.replace("{cards}", "\n".join(cards)).replace("{count}", str(len(chosen)))
    target = out_dir / f"{name}.html"
    target.write_text(page, encoding="utf-8")
    return target


def write(layout, books: list[Book], label: str = "all") -> str:
    """label names the files, e.g. "class-1", so each class's sheet is kept while the next runs."""
    out_dir = layout.root / "review"
    out_dir.mkdir(parents=True, exist_ok=True)
    text = report(layout, books)
    (out_dir / f"report-{label}.txt").write_text(text + "\n", encoding="utf-8")
    target = sheet(layout, books, out_dir, f"sheet-{label}")
    marks_path = out_dir / "marks.json"
    if marks_path.exists():
        scored = score(layout, books, json.loads(marks_path.read_text(encoding="utf-8")))
        (out_dir / "score.txt").write_text(scored + "\n", encoding="utf-8")
        text += "\n\nMarked by hand:\n" + scored
    return text + f"\n\nReview sheet: {target}"


SHEET = """<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Picture review</title>
<style>
:root { --bg:#faf9f6; --fg:#1d1d1b; --muted:#6b6a66; --line:#dddad3; --card:#fff; --accent:#2f6f5e; }
@media (prefers-color-scheme: dark) { :root { --bg:#161615; --fg:#ecebe7; --muted:#a3a19b; --line:#34332f; --card:#1f1f1d; --accent:#6fbfa6; } }
body { margin:0; padding:16px; background:var(--bg); color:var(--fg); font:15px/1.45 system-ui, sans-serif; }
header { max-width:760px; margin:0 auto 16px; }
section { max-width:760px; margin:0 auto 16px; background:var(--card); border:1px solid var(--line); border-radius:8px; padding:12px; }
h2 { font-size:13px; color:var(--muted); margin:0 0 8px; font-weight:600; }
img { max-width:100%; background:#fff; border-radius:4px; display:block; margin-bottom:8px; }
.row { display:flex; flex-wrap:wrap; gap:10px; align-items:center; padding:6px 0; border-top:1px solid var(--line); }
.row span { flex:1 1 280px; }
button { background:var(--accent); color:#fff; border:0; border-radius:6px; padding:10px 16px; font-size:15px; }
</style></head><body>
<header><h1>Picture review ({count} figures)</h1>
<p>Mark each line right or wrong against the picture. Leave a line blank if you can't tell. Marks are kept in this browser as you go; when done, press the button and save <b>marks.json</b> into the <code>review</code> folder next to this page.</p>
<button onclick="save()">Download marks.json</button></header>
{cards}
<script>
const KEY = "lantern-review-marks";
let marks = {};
try { marks = JSON.parse(localStorage.getItem(KEY) || "{}"); } catch (e) {}
for (const [name, value] of Object.entries(marks)) {
  const input = document.querySelector(`input[name="${CSS.escape(name)}"][value="${value}"]`);
  if (input) input.checked = true;
}
document.addEventListener("change", e => {
  marks[e.target.name] = e.target.value;
  try { localStorage.setItem(KEY, JSON.stringify(marks)); } catch (err) {}
});
function save() {
  const out = {};
  for (const [name, value] of Object.entries(marks)) {
    const [figure, key] = name.split("|");
    (out[figure] = out[figure] || {})[key] = value;
  }
  const link = document.createElement("a");
  link.href = URL.createObjectURL(new Blob([JSON.stringify(out, null, 1)], {type: "application/json"}));
  link.download = "marks.json";
  link.click();
}
</script></body></html>
"""
