"""Stacked fractions, rebuilt from where the glyphs sit on the page.

A PDF's text layer has no fractions: ¾ is a "3", a short horizontal rule and a "4" drawn below
it, so extraction gives "3\\n4" and a chapter on fractions reads as nonsense ("Distance covered =
3 4 × 2 5"). The page's geometry still says what it is: a number directly above another number,
with a short bar between them and all three centred on one another. That test needs no model and
doesn't guess: a bar is either there or it isn't. Found fractions are written back into the text
as "3/4", in reading order.
"""

from __future__ import annotations

import re
import unicodedata
from dataclasses import dataclass
from pathlib import Path

import pypdfium2 as pdfium
import pypdfium2.raw as pdfium_c

# A fraction bar is a thin rule (its bounds include the stroke width) no wider than a long
# denominator; numerator and denominator sit within a few points of it.
BAR_MAX_HEIGHT = 2.6
BAR_WIDTH = (2.5, 60.0)
BAR_REACH = 7.0

# NCERT's newer books set some fractions in maths bold digits (U+1D7CE…), one surrogate pair each.
_MATHS_DIGITS = {0x1D7CE + i: str(i % 10) for i in range(50)}


def ascii_digits(text: str) -> str:
    return text.translate(_MATHS_DIGITS)


@dataclass
class Bar:
    left: float
    bottom: float
    right: float
    top: float


def page_bars(page) -> list[Bar]:
    bars = []
    for obj in page.get_objects(max_depth=1):  # deeper objects have form-local coordinates
        if obj.type != pdfium_c.FPDF_PAGEOBJ_PATH:
            continue
        left, bottom, right, top = obj.get_bounds()
        if top - bottom <= BAR_MAX_HEIGHT and BAR_WIDTH[0] <= right - left <= BAR_WIDTH[1]:
            bars.append(Bar(left, bottom, right, top))
    return bars


@dataclass
class Glyph:
    index: int
    text: str
    left: float
    bottom: float
    right: float
    top: float


def page_glyphs(textpage) -> list[Glyph]:
    """Every visible character with its box; a surrogate pair (maths bold digits) is one glyph."""
    glyphs = []
    count = textpage.count_chars()
    index = 0
    while index < count:
        code = pdfium_c.FPDFText_GetUnicode(textpage, index)
        width = 1
        if 0xD800 <= code <= 0xDBFF and index + 1 < count:
            low = pdfium_c.FPDFText_GetUnicode(textpage, index + 1)
            code = 0x10000 + ((code - 0xD800) << 10) + (low - 0xDC00)
            width = 2
        char = chr(code)
        if not char.isspace():
            left, bottom, right, top = textpage.get_charbox(index)
            glyphs.append(Glyph(index, char, left, bottom, right, top))
        index += width
    return glyphs


def _side(glyphs: list[Glyph], bar: Bar, above: bool) -> list[Glyph]:
    """The glyphs standing over (or under) the bar: centred within its width, close to it. A tall
    bracket around the fraction spans the bar vertically, so it is neither above nor below."""
    picked = []
    for glyph in glyphs:
        if not bar.left - 0.5 <= (glyph.left + glyph.right) / 2 <= bar.right + 0.5:
            continue
        gap = glyph.bottom - bar.top if above else bar.bottom - glyph.top
        if -0.5 <= gap <= BAR_REACH:
            picked.append(glyph)
    return picked


def pair(textpage, bars: list[Bar], glyphs: list[Glyph] | None = None) -> list[tuple[str, str]]:
    """(numerator, denominator) for each bar with text just above and just below it, in the text
    layer's order, the order the text will be patched in. Either side may be an expression, such
    as (5 × 7) over (12 × 18), or a letter, as in a/b. Each side keeps the text layer's spelling
    so that patch can find it."""
    glyphs = page_glyphs(textpage) if glyphs is None else glyphs
    found: list[tuple[int, str, str]] = []
    for bar in bars:
        top, bottom = _side(glyphs, bar, True), _side(glyphs, bar, False)
        if not top or not bottom:
            continue  # a dash or an underline, not a fraction
        found.append((top[0].index, "".join(g.text for g in top), "".join(g.text for g in bottom)))
    return [(top, bottom) for _, top, bottom in sorted(found)]


def _loose(part: str) -> str:
    """A regex for the part as the text layer spells it: any spacing between its symbols."""
    return r"\s*".join(re.escape(c) for c in part.replace(" ", ""))


def _shown(part: str) -> str:
    part = unicodedata.normalize("NFKC", part)  # maths italic 𝑛 -> n, bold 𝟏 -> 1
    return part if re.fullmatch(r"\w+|\(.*\)", part) else f"({part})"


def patch(text: str, fractions: list[tuple[str, str]]) -> tuple[str, int]:
    """Joins each numerator-newline-denominator in the text into "n/d", in order. A fraction the
    text doesn't show that way is skipped, never forced. Returns the text and how many were
    joined."""
    out, cursor, joined = [], 0, 0
    for numerator, denominator in fractions:
        match = re.compile(
            rf"(?<![\w/]){_loose(numerator)}[ \t]*\r?\n[ \t]*{_loose(denominator)}(?![\w])"
        ).search(text, cursor)
        if match is None:
            continue
        out.append(text[cursor : match.start()])
        out.append(f"{_shown(numerator)}/{_shown(denominator)}")
        cursor = match.end()
        joined += 1
    out.append(text[cursor:])
    return "".join(out), joined


def page_fractions(pdf_path: Path) -> list[list[tuple[str, str]]]:
    """For each page, its fractions in reading order."""
    document = pdfium.PdfDocument(pdf_path)
    try:
        result = []
        for page in document:
            textpage = page.get_textpage()
            result.append(pair(textpage, page_bars(page)))
            textpage.close()
            page.close()
        return result
    finally:
        document.close()
