import cv2
import numpy as np

from ncert_build import checks
from ncert_build.figures import Box, drop_repeats
from ncert_build.render import lines, settle_counts


def white(height=400, width=500):
    return np.full((height, width, 3), 255, np.uint8)


def test_a_ruled_grid_gives_rows_columns_and_filled_cells():
    image = white()
    for row in range(3):
        for column in range(4):
            x, y = 50 + column * 100, 50 + row * 100
            if row == 0 and column < 2:
                cv2.rectangle(image, (x, y), (x + 100, y + 100), (250, 230, 60), -1)  # yellow
            if row == 0 and column == 0:
                for step in range(0, 100, 8):  # hatching
                    cv2.line(image, (x + step, y), (x, y + step), (0, 0, 0), 2)
    grey = (175, 175, 175)  # NCERT's inner lines are light grey
    for row in range(4):
        cv2.line(image, (50, 50 + row * 100), (450, 50 + row * 100), (0, 0, 0) if row in (0, 3) else grey, 2)
    for column in range(5):
        cv2.line(image, (50 + column * 100, 50), (50 + column * 100, 350), (0, 0, 0) if column in (0, 4) else grey, 2)
    assert checks.grid(image) == checks.Grid(rows=3, columns=4, coloured=2, hatched=1)


def test_a_box_around_a_drawing_is_not_a_grid():
    image = white()
    cv2.rectangle(image, (50, 50), (450, 350), (0, 0, 0), 2)
    cv2.circle(image, (250, 200), 60, (0, 0, 0), 3)
    assert checks.grid(image) is None


def test_separate_drawings_of_one_size_are_counted():
    image = white()
    for i in range(5):
        cv2.circle(image, (60 + i * 90, 200), 30, (40, 40, 40), -1)
    assert checks.blobs(image) == 5


def test_drawings_of_very_different_sizes_are_not_counted():
    image = white()
    cv2.circle(image, (80, 200), 60, (40, 40, 40), -1)
    cv2.circle(image, (300, 200), 8, (40, 40, 40), -1)
    cv2.circle(image, (400, 200), 9, (40, 40, 40), -1)
    assert checks.blobs(image) is None


def test_decoration_repeated_on_many_pages_is_dropped():
    footer = Box(0, 0, 590, 40)
    pages = {n: [footer] for n in range(1, 6)}
    pages[3] = [footer, Box(100, 300, 300, 500)]
    assert drop_repeats(pages) == {1: [], 2: [], 3: [Box(100, 300, 300, 500)], 4: [], 5: []}


def group(obj, count):
    return {"object": obj, "how": "", "count": count, "sure": True}


def test_a_count_the_pixel_check_agrees_with_is_accepted():
    assert settle_counts([[group("elephants", 5)]], check=5)[0]["agreed"] == "check"


def test_the_majority_of_three_reads_stands():
    settled = settle_counts([[group("Elephants", 3)], [group("elephant", 5)], [group("elephant", 5)]], None)
    assert settled[0]["count"] == 5 and settled[0]["sure"]


def test_three_different_reads_leave_the_count_unsure():
    settled = settle_counts([[group("bird", 3)], [group("bird", 4)], [group("bird", 6)]], None)
    assert settled[0]["count"] == 3 and not settled[0]["sure"]


def test_figure_lines_are_short_and_mark_what_is_unsure():
    figure = {"page": 6, "figure": 2, "kind": "grid", "summary": "2/5 of a unit square split into 4 parts.",
              "labels": ["whole", "2/5"], "grid": {"rows": 5, "columns": 4, "coloured": 6, "hatched": 2, "sure": False}}
    assert lines(figure) == (
        "[fig p6.2 grid] 2/5 of a unit square split into 4 parts.\n"
        "labels: whole · 2/5\n"
        "grid: 5 rows × 4 columns, 6 coloured, 2 hatched (unsure)"
    )
