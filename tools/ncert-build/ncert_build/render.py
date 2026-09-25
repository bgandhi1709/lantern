"""What the pictures teach, read by a local vision model, one figure at a time.

Younger classes teach on the page's pictures: tally sticks to count, a row of elephants, a number
chart with blanks, a unit square with a fraction shaded. The text layer has none of it. This stage
finds each figure (see figures), crops it sharper than a page render, and has the vision model on
the GPU read it in two steps:

1. What kind of figure it is, its printed labels and one line on what it shows. The kinds are
   NCERT's recurring picture types; decoration is dropped.
2. For kinds where a number is the lesson (objects, tally sticks, dots, fingers, beads, grids), a
   count written out before it is given ("row 1: 5 elephants, row 2: 3 houses"), with an
   instruction for that kind: tally sticks are counted as bundles of five plus singles.

A count is accepted when an independent check agrees (checks: grid lines, separate drawings).
Otherwise the model counts again twice at a bigger zoom and the majority stands; with no majority
the count is marked unsure. Claude then reads a few short lines per figure instead of opening an
image, and knows which numbers to trust.
"""

from __future__ import annotations

import io
import re
import urllib.error
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

import numpy as np
import pypdfium2 as pdfium

from . import checks
from .figures import Box, chapter_figures, crop

PROMPT_VERSION = "picture-v2"
NUM_CTX = 8192
READ_DPI = 200
RECOUNT_DPI = 280
MAX_SIDE = 1024  # pixels; a bigger crop is rendered at a lower dpi
SAVE_DPI = 110  # the copy kept on disk, for the review sheet and the rare page Claude opens

# ask(prompt, png bytes, JSON schema, temperature) -> the model's JSON reply
AskFn = Callable[[str, bytes, dict, float], dict]

KINDS = {
    "objects": "separate drawings of things to count (animals, fruit, toys), maybe in rows or groups",
    "tally": "tally marks or sticks/logs used for counting, single or tied in bundles",
    "dots": "dots, counters, ten-frames or dice faces",
    "fingers": "hands showing numbers with fingers",
    "beads": "beads on a string or an abacus",
    "grid": "a ruled grid or unit square, maybe with some cells coloured or hatched",
    "numbers": "printed numerals, number cards or digits to read, with nothing drawn to count",
    "number_line": "a number line with marks, jumps or arrows",
    "number_chart": "a chart or table of numbers, maybe with blanks to fill",
    "table": "a table or tally chart of words and numbers",
    "graph": "a bar graph, pictograph or pie chart",
    "shapes": "geometric shapes, patterns of shapes, angles or measurements",
    "clock": "a clock face",
    "money": "coins or notes",
    "scene": "a picture of people, animals or places telling a story or situation",
    "diagram": "a labelled science diagram, process or apparatus",
    "map": "a map",
    "decoration": "a border, background panel, icon or ornament that teaches nothing",
}
COUNTED = {"objects", "tally", "dots", "fingers", "beads", "money"}
HOW_TO_COUNT = {
    "objects": "Count each kind of object separately. If they are in rows or boxes, count row by "
    "row: 'row 1: 1, 2, 3, 4, 5 elephants'.",
    "tally": "Each tied bundle is 5 or 10 sticks: check which by counting one bundle's sticks. "
    "Count bundles, then loose sticks: '2 bundles of 5 + 3 loose = 13'.",
    "dots": "Count dots in each frame, row or group separately, then give each group's total.",
    "fingers": "For each hand or pair of hands, count raised fingers: 'left hand 5 + right hand 2 = 7'.",
    "beads": "Count beads of each colour or on each rod separately.",
    "money": "List each coin or note with its printed value, then give the number of each.",
}

DESCRIBE = """This is one figure cut from a Class {grade} NCERT {subject} textbook page.
kind: the one that fits best:
{kinds}
labels: every number, word and sum printed in the figure, exactly, in reading order. Write a blank
box or missing number as _. Fractions as 3/4. Leave out the watermark.
summary: one sentence, at most 25 words, on what the figure shows or asks the child to do. Say
only what is visible. Don't solve anything."""

COUNT = """This is one figure cut from a Class {grade} NCERT {subject} textbook page. It shows {kind}.
Count what is drawn. {how}
For each kind of thing or each group, write how you counted first, then the count.
Count only what is drawn, not numbers printed in the figure: printed numbers are often what the
child has to find. If you can't see well enough to be sure, set sure to false."""

GRID = """This is one figure cut from a Class {grade} NCERT {subject} textbook page: a grid or unit
square. Count its rows and columns of cells, one row at a time. coloured: every cell filled with a
colour, including cells that are also striped. hatched: every cell with stripes."""

DESCRIBE_SCHEMA = {
    "type": "object",
    "properties": {
        "kind": {"type": "string", "enum": list(KINDS)},
        "labels": {"type": "array", "items": {"type": "string"}},
        "summary": {"type": "string"},
    },
    "required": ["kind", "labels", "summary"],
}
COUNT_SCHEMA = {
    "type": "object",
    "properties": {
        "groups": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "object": {"type": "string"},
                    "how": {"type": "string"},
                    "count": {"type": "integer"},
                    "sure": {"type": "boolean"},
                },
                "required": ["object", "how", "count", "sure"],
            },
        }
    },
    "required": ["groups"],
}
GRID_SCHEMA = {
    "type": "object",
    "properties": {
        "how": {"type": "string"},
        "rows": {"type": "integer"},
        "columns": {"type": "integer"},
        "coloured": {"type": "integer"},
        "hatched": {"type": "integer"},
    },
    "required": ["how", "rows", "columns", "coloured", "hatched"],
}


def _png(image) -> bytes:
    buffer = io.BytesIO()
    image.save(buffer, format="PNG", optimize=True)
    return buffer.getvalue()


def _fitted(page, box: Box, dpi: int):
    longest = max(box.right - box.left, box.top - box.bottom) + 12
    return crop(page, box, min(dpi, int(MAX_SIDE * 72 / longest)))


def _name(text: str) -> str:
    """'Elephants ' -> 'elephant', so counts from different reads can be matched."""
    word = " ".join(text.lower().split())
    return re.sub(r"(?<=[a-z]{3})(es|s)$", "", word)


@dataclass
class Reader:
    ask: AskFn
    grade: int
    subject: str

    def describe(self, png: bytes) -> dict:
        kinds = "\n".join(f"- {k}: {v}" for k, v in KINDS.items())
        prompt = DESCRIBE.format(grade=self.grade, subject=self.subject, kinds=kinds)
        return self.ask(prompt, png, DESCRIBE_SCHEMA, 0.1)

    def count(self, png: bytes, kind: str, temperature: float) -> list[dict]:
        prompt = COUNT.format(grade=self.grade, subject=self.subject, kind=KINDS[kind], how=HOW_TO_COUNT.get(kind, ""))
        return self.ask(prompt, png, COUNT_SCHEMA, temperature)["groups"]

    def grid(self, png: bytes, temperature: float) -> dict:
        return self.ask(GRID.format(grade=self.grade, subject=self.subject), png, GRID_SCHEMA, temperature)


def settle_counts(reads: list[list[dict]], check: int | None) -> list[dict]:
    """One count per object from up to three reads. The first read stands if the pixel check
    agrees with it (only possible with a single group) or if nothing disagrees; otherwise the
    majority of the reads, and without one, the first read marked unsure."""
    first = reads[0]
    if len(first) == 1 and check is not None and first[0]["count"] == check:
        return [{**first[0], "sure": True, "agreed": "check"}]
    settled = []
    for group in first:
        name = _name(group["object"])
        votes = [g["count"] for read in reads for g in read if _name(g["object"]) == name]
        value, times = Counter(votes).most_common(1)[0]
        if len(reads) == 1:
            settled.append({**group, "agreed": "none"})
        elif times >= 2:
            settled.append({**group, "count": value, "sure": True, "agreed": f"{times}/{len(reads)} reads"})
        else:
            settled.append({**group, "sure": False, "agreed": f"reads differ: {votes}"})
    return settled


def read_figure(reader: Reader, page, box: Box) -> dict | None:
    sharp = _fitted(page, box, READ_DPI)
    png = _png(sharp)
    described = reader.describe(png)
    kind = described["kind"]
    if kind == "decoration":
        return None
    result = {"kind": kind, "labels": described["labels"], "summary": described["summary"].strip()}
    pixels = np.asarray(sharp)
    if kind == "grid" or kind == "shapes":
        seen = checks.grid(pixels)
        if kind == "grid" or seen is not None:
            result["grid"] = _settle_grid(reader, page, box, png, seen)
    if kind in COUNTED:
        blob_count = checks.blobs(pixels) if kind in ("objects", "dots") else None
        reads = [reader.count(png, kind, 0.1)]
        settled = settle_counts(reads, blob_count)
        if any(not g.get("agreed") == "check" for g in settled) and reads[0]:
            zoomed = _png(_fitted(page, box, RECOUNT_DPI))
            reads += [reader.count(zoomed, kind, 0.1), reader.count(zoomed, kind, 0.5)]
            settled = settle_counts(reads, None)
        result["counts"] = settled
    return result


def _settle_grid(reader: Reader, page, box: Box, png: bytes, seen: checks.Grid | None) -> dict:
    """The pixel check measures ruled lines and fills exactly, but a table of pictures can look
    like a grid to it; so it stands only when the model sees the same rows and columns."""
    fields = ("rows", "columns", "coloured", "hatched")
    first = reader.grid(png, 0.1)
    if seen is not None and (first["rows"], first["columns"]) == (seen.rows, seen.columns):
        return {**{f: getattr(seen, f) for f in fields}, "sure": True, "agreed": "check"}
    second = reader.grid(_png(_fitted(page, box, RECOUNT_DPI)), 0.1)
    agree = all(first[f] == second[f] for f in fields)
    note = "2 reads" if agree else f"reads differ: {second}"
    if seen is not None:
        note += f"; check saw {seen.rows}×{seen.columns}"
    return {**{f: first[f] for f in fields}, "sure": agree and seen is None, "agreed": note}


def read_chapter(pdf_path: Path, reader: Reader, image_dir: Path, pages: list[int] | None = None) -> list[dict]:
    """Every figure in the chapter, read. The kept image of each is written to image_dir."""
    found = chapter_figures(pdf_path, pages)
    document = pdfium.PdfDocument(pdf_path)
    out = []
    try:
        for number, boxes in sorted(found.items()):
            page = document[number - 1]
            for position, box in enumerate(sorted(boxes, key=lambda b: (-b.top, b.left)), start=1):
                try:
                    figure = read_figure(reader, page, box)
                # One unreadable figure must not lose the chapter. A server error (HTTP 500) is a
                # figure Ollama fails on; a connection error still stops the chapter.
                except (ValueError, urllib.error.HTTPError) as error:
                    figure = {"kind": "unread", "labels": [], "summary": f"not read: {error}"}
                if figure is None:
                    continue
                image = image_dir / f"p{number}-{position}.png"
                image.parent.mkdir(parents=True, exist_ok=True)
                crop(page, box, SAVE_DPI).save(image, optimize=True)
                out.append({"page": number, "figure": position, "image": str(image),
                            "box": [round(v, 1) for v in (box.left, box.bottom, box.right, box.top)], **figure})
            page.close()
    finally:
        document.close()
    return out


def lines(figure: dict) -> str:
    """The figure as Claude reads it in the bundle: a few short lines, unsure numbers marked."""
    head = f"[fig p{figure['page']}.{figure['figure']} {figure['kind']}]"
    parts = [f"{head} {figure['summary']}"]
    if figure.get("labels"):
        parts.append("labels: " + " · ".join(figure["labels"]))
    grid = figure.get("grid")
    if grid:
        mark = "" if grid["sure"] else " (unsure)"
        parts.append(f"grid: {grid['rows']} rows × {grid['columns']} columns, {grid['coloured']} coloured, "
                     f"{grid['hatched']} hatched{mark}")
    if figure.get("counts"):
        counted = [f"{g['object']} {g['count']}" + ("" if g["sure"] else " (unsure)") for g in figure["counts"]]
        parts.append("counts: " + "; ".join(counted))
    return "\n".join(parts)
