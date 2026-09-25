"""Stage 3: the PDF's own text layer, page by page, with quality flags (no OCR, D16).

Symbol-font glyphs (θ, Δ, ∠, −) are decoded back to Unicode on the way in; see symbol_font.
Stacked fractions are rebuilt from the page geometry ("3\n4" becomes "3/4"); see fractions.

Flags mark the pages where plain text is not good enough, so a later, optional vision pass can be
limited to them instead of reading every page.
"""

from __future__ import annotations

import hashlib
import json
import unicodedata
from dataclasses import asdict, dataclass, field
from pathlib import Path

import pypdfium2 as pdfium
from pypdf import PdfReader, apply_configuration

from . import fractions, symbol_font

# Bumped when the text itself changes, so older extractions are redone (text-v2: fractions).
EXTRACT_VERSION = "text-v2"
LOW_TEXT_CHARS = 200
MATHS_DENSE_RATIO = 0.18
GARBLED_RATIO = 0.05
# pypdf stops at 75 MB of decompressed data per stream to guard against hostile files. A few
# NCERT chapters (Class 10 Science, chapter 8) embed images larger than that. These PDFs come from
# NCERT only, so the limit is raised for them rather than losing the chapter.
MAX_DECOMPRESSED_BYTES = 400_000_000
_MATHS_CHARS = set("0123456789+-−×÷=<>≤≥%/()[]^√π")


@dataclass
class Page:
    number: int
    text: str
    flags: list[str] = field(default_factory=list)


def quality_flags(text: str) -> list[str]:
    stripped = "".join(text.split())
    if len(stripped) < LOW_TEXT_CHARS:
        # Mostly pictures, or a scanned page with no text layer.
        return ["low_text"]
    flags = []
    maths = sum(1 for c in stripped if c in _MATHS_CHARS)
    if maths / len(stripped) >= MATHS_DENSE_RATIO:
        flags.append("maths_dense")
    # Private-use and replacement characters are what legacy-encoded fonts turn into.
    garbled = sum(1 for c in stripped if c == "�" or unicodedata.category(c) in ("Co", "Cn"))
    if garbled / len(stripped) >= GARBLED_RATIO:
        flags.append("garbled")
    return flags


def extract_pdf(pdf_path: Path) -> list[Page]:
    pages = []
    geometry = pdfium.PdfDocument(pdf_path)
    try:
        with apply_configuration(zlib_maximum_output_length=MAX_DECOMPRESSED_BYTES):
            reader = PdfReader(pdf_path)
            for index, pdf_page in enumerate(reader.pages, start=1):
                text = pdf_page.extract_text() or ""
                if index <= len(geometry):
                    drawn = geometry[index - 1]
                    text, _ = fractions.patch(text, fractions.pair(drawn.get_textpage(), fractions.page_bars(drawn)))
                text = fractions.ascii_digits(unicodedata.normalize("NFC", symbol_font.decode(text)))
                pages.append(Page(number=index, text=text, flags=quality_flags(text)))
    finally:
        geometry.close()
    return pages


def write(pdf_path: Path, pages: list[Page], target: Path) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    document = {
        "version": EXTRACT_VERSION,
        "source": pdf_path.name,
        "source_sha256": hashlib.sha256(pdf_path.read_bytes()).hexdigest(),
        "pages": [asdict(p) for p in pages],
        "pages_needing_vision": [p.number for p in pages if p.flags],
    }
    target.write_text(json.dumps(document, indent=2, ensure_ascii=False), encoding="utf-8")
