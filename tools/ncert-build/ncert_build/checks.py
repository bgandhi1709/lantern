"""Second opinions on what the vision model counts, from the pixels alone.

A small vision model reads what a figure is well but miscounts. These checks count the two
things NCERT figures count most, with plain image processing that can't hallucinate:

- grid: the ruled lines of a unit square or a table give its rows and columns, and each cell's
  fill says whether it is coloured (NCERT shades one fraction yellow) or hatched (the product).
- blobs: separate drawings of about the same size on white paper (five elephants in a row) are
  separate ink regions.

Each check answers only when the picture is clearly the kind it knows, and None otherwise. Where
a check and the model agree, the count is trusted; where they differ, the model is asked again.
"""

from __future__ import annotations

from dataclasses import dataclass

import cv2
import numpy as np

from .figures import ink_mask

DARK = 140  # hatching strokes
LINE_GREY = 205  # grid lines: NCERT draws inner lines light grey, but greyer than the watermark
LINE_MAX_SATURATION = 60  # a coloured fill is not a line
LINE_SHARE = 0.4  # of the longest line in that direction
SAME_LINE = 5  # pixels: stroke width and anti-aliasing
DASH_GAP = 15  # pixels: gaps in a dashed divider
EVEN_SPACING = 0.25  # most spread between gaps allowed, as a share of the median gap
COLOURED_SATURATION = 60
HATCHED_INK = 0.05  # share of a cell covered by stripes; plain cells measure 0
BLOB_GROW = 3
BLOB_MIN_SHARE = 0.004  # of the picture
BLOB_SIZE_RANGE = (0.35, 2.8)  # of the median blob: the same object drawn again


@dataclass(frozen=True)
class Grid:
    rows: int
    columns: int
    coloured: int
    hatched: int


def _positions(mask: np.ndarray, axis: int) -> list[int]:
    """Where the lines of a line mask sit across ``axis`` (0: y of horizontal lines)."""
    lengths = mask.sum(axis=1 - axis) / 255
    if lengths.max(initial=0) == 0:
        return []
    rows = np.where(lengths >= LINE_SHARE * lengths.max())[0]
    positions: list[list[int]] = []
    for row in rows:
        if positions and row - positions[-1][-1] <= SAME_LINE:
            positions[-1].append(int(row))
        else:
            positions.append([int(row)])
    return [int(np.mean(group)) for group in positions]


def _even_run(positions: list[int]) -> list[int]:
    """The longest run of evenly spaced lines: the grid itself, without the measuring rules,
    braces and arrows drawn beside it."""
    best: list[int] = positions[:1]
    for start in range(len(positions) - 1):
        gap = positions[start + 1] - positions[start]
        if gap <= 2 * SAME_LINE:
            continue
        run = positions[start : start + 2]
        for position in positions[start + 2 :]:
            if abs(position - run[-1] - gap) > EVEN_SPACING * gap:
                break
            run.append(position)
        if len(run) > len(best) or (len(run) == len(best) and run[-1] - run[0] > best[-1] - best[0]):
            best = run
    return best


def grid(rgb: np.ndarray) -> Grid | None:
    """Rows, columns and filled cells of the one evenly ruled grid in the picture, if there is
    one."""
    grey = cv2.cvtColor(rgb, cv2.COLOR_RGB2GRAY)
    saturation = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)[:, :, 1]
    dark = np.where(grey < DARK, 255, 0).astype(np.uint8)
    # A line is dark (even where it runs through a coloured fill), or light grey and uncoloured.
    ruled = np.where((grey < DARK) | ((grey < LINE_GREY) & (saturation < LINE_MAX_SATURATION)), 255, 0)
    ruled = ruled.astype(np.uint8)
    height, width = dark.shape
    # Close the gaps of dashed dividers first, then keep only long straight runs: hatching,
    # letters and drawings fall away.
    across = cv2.morphologyEx(ruled, cv2.MORPH_CLOSE, np.ones((1, DASH_GAP), np.uint8))
    down = cv2.morphologyEx(ruled, cv2.MORPH_CLOSE, np.ones((DASH_GAP, 1), np.uint8))
    horizontal = cv2.morphologyEx(across, cv2.MORPH_OPEN, np.ones((1, max(8, width // 8)), np.uint8))
    vertical = cv2.morphologyEx(down, cv2.MORPH_OPEN, np.ones((max(8, height // 8), 1), np.uint8))
    ys, xs = _even_run(_positions(horizontal, 0)), _even_run(_positions(vertical, 1))
    if len(ys) < 2 or len(xs) < 2:
        return None
    if len(ys) == 2 and len(xs) == 2:
        return None  # a plain box around something, not a grid
    coloured = hatched = 0
    margin = SAME_LINE + 1
    for top, bottom in zip(ys, ys[1:]):
        for left, right in zip(xs, xs[1:]):
            inside = (slice(top + margin, bottom - margin), slice(left + margin, right - margin))
            if saturation[inside].size == 0:
                continue
            # A hatched cell is usually coloured too: both are counted.
            if np.mean(dark[inside]) / 255 >= HATCHED_INK:
                hatched += 1
            paper = dark[inside] == 0
            if paper.any() and np.mean(saturation[inside][paper]) >= COLOURED_SATURATION:
                coloured += 1
    return Grid(len(ys) - 1, len(xs) - 1, coloured, hatched)


def blobs(rgb: np.ndarray) -> int | None:
    """How many separate, similar-sized drawings the picture holds, or None when its ink doesn't
    split that way (one scene, or drawings of very different sizes)."""
    mask = ink_mask(rgb)
    mask = cv2.dilate(mask, np.ones((BLOB_GROW, BLOB_GROW), np.uint8))
    count, _, stats, _ = cv2.connectedComponentsWithStats(mask, connectivity=8)
    areas = stats[1:count, cv2.CC_STAT_AREA]
    areas = areas[areas >= BLOB_MIN_SHARE * mask.size]
    if len(areas) < 2:
        return None
    median = float(np.median(areas))
    low, high = BLOB_SIZE_RANGE
    alike = int(np.sum((areas >= low * median) & (areas <= high * median)))
    return alike if alike >= 0.8 * len(areas) else None
