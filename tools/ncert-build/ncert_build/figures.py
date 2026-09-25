"""Where a page's pictures are, found without a model.

A page is rendered small, its text is blanked out using the text layer's own glyph boxes, and what
ink is left is grown into blobs: each large blob is a figure (a unit-square grid, a row of
elephants, a number chart). Coloured fills count as ink even when they are light, but the grey
"not to be republished" watermark doesn't. Decoration printed on many pages at the same place (the
footer wave, the page-number disc) is dropped, the way segment drops running headers.

Only the figures go to the vision model, each cropped and rendered sharper, so a small model
reads one thing at a time instead of a whole page.
"""

from __future__ import annotations

from dataclasses import dataclass

import cv2
import numpy as np
import pypdfium2 as pdfium

from .fractions import page_bars, page_glyphs

FIND_DPI = 50
INK_GREY = 200  # darker than the watermark
INK_SATURATION = 60  # coloured fills (yellow shading) are light but saturated
GROW_POINTS = 7.0  # parts of one drawing closer than this are one figure
MIN_AREA_SHARE = 0.012  # of the page
MIN_SIDE_POINTS = 28.0
PAD_POINTS = 6.0
REPEAT_MIN_PAGES = 3
REPEAT_MIN_SHARE = 0.3


@dataclass(frozen=True)
class Box:
    """In PDF points, origin at the page's bottom left."""

    left: float
    bottom: float
    right: float
    top: float

    @property
    def area(self) -> float:
        return max(0.0, self.right - self.left) * max(0.0, self.top - self.bottom)

    def overlap(self, other: "Box") -> float:
        """Intersection over union."""
        width = min(self.right, other.right) - max(self.left, other.left)
        height = min(self.top, other.top) - max(self.bottom, other.bottom)
        if width <= 0 or height <= 0:
            return 0.0
        shared = width * height
        return shared / (self.area + other.area - shared)


def ink_mask(rgb: np.ndarray) -> np.ndarray:
    grey = cv2.cvtColor(rgb, cv2.COLOR_RGB2GRAY)
    saturation = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)[:, :, 1]
    return ((grey < INK_GREY) | (saturation > INK_SATURATION)).astype(np.uint8)


def blobs(mask: np.ndarray, scale: float, page_height: float) -> list[Box]:
    """Large connected ink regions of a mask drawn at ``scale`` pixels per point."""
    grow = max(1, int(GROW_POINTS * scale))
    grown = cv2.dilate(mask, np.ones((grow, grow), np.uint8))
    count, _, stats, _ = cv2.connectedComponentsWithStats(grown, connectivity=8)
    height, width = mask.shape
    boxes = []
    for x, y, w, h, _ in stats[1:count]:
        w, h = w - grow, h - grow  # take back what growing added before judging the size
        if w * h < MIN_AREA_SHARE * width * height or min(w, h) < MIN_SIDE_POINTS * scale:
            continue
        x, y = x + grow / 2, y + grow / 2
        left, right = x / scale, (x + w) / scale
        top, bottom = page_height - y / scale, page_height - (y + h) / scale
        boxes.append(Box(left, bottom, right, top))
    return boxes


def page_figures(page) -> list[Box]:
    scale = FIND_DPI / 72
    page_width, page_height = page.get_size()
    rgb = np.asarray(page.render(scale=scale).to_pil().convert("RGB"))
    mask = ink_mask(rgb)
    textpage = page.get_textpage()
    # Text and fraction bars are read from the text layer already; what's left is drawn.
    for part in [*page_glyphs(textpage), *page_bars(page)]:
        x0, x1 = int(part.left * scale) - 1, int(part.right * scale) + 2
        y0, y1 = int((page_height - part.top) * scale) - 1, int((page_height - part.bottom) * scale) + 2
        mask[max(0, y0) : y1, max(0, x0) : x1] = 0
    textpage.close()
    return blobs(mask, scale, page_height)


def drop_repeats(pages: dict[int, list[Box]]) -> dict[int, list[Box]]:
    """Removes boxes found at the same place on many pages: printed decoration, not teaching."""
    total = len(pages)
    kept: dict[int, list[Box]] = {}
    for number, boxes in pages.items():
        kept[number] = []
        for box in boxes:
            repeats = sum(any(box.overlap(o) > 0.7 for o in others) for n, others in pages.items() if n != number)
            if repeats + 1 >= REPEAT_MIN_PAGES and (repeats + 1) / total >= REPEAT_MIN_SHARE:
                continue
            kept[number].append(box)
    return kept


def chapter_figures(pdf_path, pages: list[int] | None = None) -> dict[int, list[Box]]:
    """Page number -> its figures, for every page (or only ``pages``). Repeats are judged over
    the whole chapter either way."""
    document = pdfium.PdfDocument(pdf_path)
    try:
        found = {}
        for number in range(1, len(document) + 1):
            page = document[number - 1]
            found[number] = page_figures(page)
            page.close()
    finally:
        document.close()
    kept = drop_repeats(found)
    return {n: kept[n] for n in (pages if pages is not None else kept) if n in kept}


def crop(page, box: Box, dpi: int):
    """The figure, padded a little, as a PIL image at ``dpi``."""
    page_width, page_height = page.get_size()
    left, right = max(0.0, box.left - PAD_POINTS), min(page_width, box.right + PAD_POINTS)
    bottom, top = max(0.0, box.bottom - PAD_POINTS), min(page_height, box.top + PAD_POINTS)
    return page.render(
        scale=dpi / 72, crop=(left, bottom, page_width - right, page_height - top)
    ).to_pil().convert("RGB")
