from ncert_build.segment import clean_pages, guess_title, segment, split_sections, undouble

# Shaped like the text pypdf gives for Class 10 Maths, chapter 1 (jemh101).
MATHS_PAGES = [
    "REAL NUMBERS 1\n1\n1.1 Introduction\nIn Class IX, you began your exploration of real numbers.\n"
    "Chapter 1.indd 1 1/17/2025 12:09:01 PM\nReprint 2026-27",
    "2 MATHEMATICS\n1.2 The Fundamental Theorem of Arithmetic\nAny natural number is a product of primes.\n"
    "Reprint 2026-27",
    "REAL NUMBERS 3\nTake any collection of prime numbers.\nEXERCISE 1.1\n1. Express 140 as a product of primes.\n"
    "Reprint 2026-27",
    "4 MATHEMATICS\n1.3 Revisiting Irrational Numbers\nA number is irrational if it cannot be written as p/q.\n"
    "Reprint 2026-27",
    "REAL NUMBERS 5\n1.4 Summary\nIn this chapter, you have studied the following points.\nReprint 2026-27",
]


def test_undouble_fixes_bold_text_drawn_twice():
    assert undouble("Let us DoLet us Do") == "Let us Do"
    assert undouble("Come, come, come") == "Come, come, come"
    assert undouble("aa") == "aa"  # too short to be a doubled heading


def test_cleaning_drops_print_slugs_and_running_headers():
    pages = clean_pages(MATHS_PAGES)
    flat = [line for page in pages for line in page]
    assert not any("indd" in l or l.startswith("Reprint") for l in flat)
    assert "2 MATHEMATICS" not in flat and "4 MATHEMATICS" not in flat
    assert "REAL NUMBERS 3" not in flat
    assert "1.2 The Fundamental Theorem of Arithmetic" in flat


def test_numbered_headings_become_sections():
    sections = split_sections(clean_pages(MATHS_PAGES))
    assert [(s.kind, s.number) for s in sections] == [
        ("section", "1.1"),
        ("section", "1.2"),
        ("exercise", "1.1"),
        ("section", "1.3"),
        ("summary", None),
    ]
    exercise = next(s for s in sections if s.kind == "exercise")
    assert exercise.pages == [3]
    assert "Express 140" in exercise.text


def test_books_without_headings_fall_back_to_pages():
    pages = [["Let us Sing", "Looking for my furry cat!"], [], ["Help Suwali sort the buttons."]]
    sections = split_sections(pages)
    assert [(s.kind, s.pages) for s in sections] == [("page", [1]), ("page", [3])]


def test_title_guess_strips_the_glued_chapter_number():
    assert guess_title(["Finding the", "Furry Cat!1", "Let us Sing"]) == "Finding the Furry Cat!"
    assert guess_title(["Unit 1", "Our Families and Communities"]) == "Our Families and Communities"
    assert guess_title(["Chapter", "Celebrating", "Festivals", "My name is Rishi."]) == "Celebrating Festivals"


def test_facing_page_numbers_are_print_slugs():
    assert clean_pages(["34 35\nChapter\nCelebrating"]) == [["Chapter", "Celebrating"]]


def test_segment_carries_book_context_and_flags():
    extracted = {"pages": [{"text": t} for t in MATHS_PAGES], "pages_needing_vision": [3], "source_sha256": "abc"}
    book = {"book_id": "jemh1", "grade": 10, "subject": "Mathematics", "title": "Mathematics"}
    chapter = segment(extracted, "jemh101", book)
    assert chapter["grade"] == 10 and chapter["chapter_id"] == "jemh101"
    assert chapter["pages_needing_vision"] == [3]
    assert chapter["sections"][0]["heading"] == "Introduction"
    assert chapter["title_guess"] == "REAL NUMBERS"
