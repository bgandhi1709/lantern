"""Stage 3: the PDF's own text layer, page by page, with quality flags (no OCR, D16).

Symbol-font glyphs (θ, Δ, ∠, −) are decoded back to Unicode on the way in; see symbol_font.

Flags mark the pages where plain text is not good enough, so a later, optional vision pass can be
limited to them instead of reading every page.
"""

from __future__ import annotations

import hashlib
import json
import unicodedata
from dataclasses import asdict, dataclass, field
from pathlib import Path

from pypdf import PdfReader

from . import symbol_font

LOW_TEXT_CHARS = 200
MATHS_DENSE_RATIO = 0.18
GARBLED_RATIO = 0.05
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
    reader = PdfReader(pdf_path)
    pages = []
    for index, pdf_page in enumerate(reader.pages, start=1):
        text = unicodedata.normalize("NFC", symbol_font.decode(pdf_page.extract_text() or ""))
        pages.append(Page(number=index, text=text, flags=quality_flags(text)))
    return pages


def write(pdf_path: Path, pages: list[Page], target: Path) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    document = {
        "source": pdf_path.name,
        "source_sha256": hashlib.sha256(pdf_path.read_bytes()).hexdigest(),
        "pages": [asdict(p) for p in pages],
        "pages_needing_vision": [p.number for p in pages if p.flags],
    }
    target.write_text(json.dumps(document, indent=2, ensure_ascii=False), encoding="utf-8")
