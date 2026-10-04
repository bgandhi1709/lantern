import pytest

from ncert_build.catalog import Book
from ncert_build.split import SplitError, find_contents, place, printed_number_offset, read_contents

# Std 3 Maths contents page, as its text layer reads.
INDEX = """* Index *
Sr.No. Lesson Name Page No.
1 Find Shapes Around Us 1-6
2 Play with Numbers 7-24
3 Addition and Subtraction 25-36
4 Fractions 37-46
5 Length and Weight 47-53
6 Data Handling 54-58"""

# A curriculum table from the same front matter: page numbers, but no contents heading.
CURRICULUM = """Curriculum Goal: CG-1 Explores the natural environment.
Provides information regarding the lifespan of plants. 36, 37
Provides information regarding the lifespan of animals. 102
Names the internal organs of the body. 15"""

BOOK = Book("ssc3-maths", 3, "Mathematics", "Mathematics Standard 3", "English", 1, 0, False, board="ssc")


def test_finds_the_contents_page_not_the_curriculum_table():
    assert find_contents(["cover", CURRICULUM, INDEX, "1 Find Shapes Around Us"]) == 2
    assert find_contents(["cover", CURRICULUM, ""]) is None


def test_offset_comes_from_the_printed_page_numbers():
    texts = ["cover", INDEX] + [f"{n} lesson text here {n}" for n in range(1, 30)]
    assert printed_number_offset(texts, after=1) == 1


def test_places_each_chapter_where_its_title_is_and_ends_it_before_the_next():
    texts = ["cover", INDEX, "1 Find Shapes Around Us", "2 more shapes", "3 Play with Numbers", "4 counting"]
    chapters = [
        {"number": "1", "title": "Find Shapes Around Us", "printed_page": 1, "lessons": []},
        {"number": "2", "title": "Play with Numbers", "printed_page": 3, "lessons": []},
    ]
    placed = place(chapters, texts, offset=1, after=1)
    assert [c["pdf_pages"] for c in placed] == [[3, 4], [5, 6]]


def test_a_chapter_with_no_title_and_no_matching_page_number_fails_the_book():
    texts = ["cover", INDEX, "something else", "other", "more"]
    with pytest.raises(SplitError):
        place([{"number": "1", "title": "Find Shapes Around Us", "printed_page": 1, "lessons": []}], texts, 1, after=1)


def test_a_title_the_model_invented_is_rejected():
    def chat(system, user, schema):
        return {"chapters": [{"number": "1", "title": "Magic Squares", "printed_page": 1, "lessons": []}]}

    with pytest.raises(SplitError, match="Magic Squares"):
        read_contents(BOOK, "lesson", ["cover", INDEX], None, chat, None)


def test_a_printed_typo_still_matches_the_title_the_model_corrected():
    from ncert_build.split import title_on_page

    assert title_on_page("Our Community Life", "8 Our Commnuity Life 87")
    assert not title_on_page("Magic Squares", "8 Our Commnuity Life 87")


def test_the_pages_own_heading_is_not_a_chapter():
    def chat(system, user, schema):
        return {
            "chapters": [
                {"number": "", "title": "INDEX", "printed_page": 0, "lessons": []},
                {"number": "1", "title": "Find Shapes Around Us", "printed_page": 1, "lessons": []},
            ]
        }

    chapters, index, checked = read_contents(BOOK, "lesson", ["cover", INDEX], None, chat, None)
    assert [c["title"] for c in chapters] == ["Find Shapes Around Us"] and (index, checked) == (1, True)


def test_a_mispaired_page_number_is_found_by_scanning_to_the_next_chapter():
    texts = ["cover", INDEX] + [f"{n} body text" for n in range(1, 11)]
    texts[6] = "5 Play with Numbers"
    chapters = [
        {"number": "1", "title": "Find Shapes Around Us", "printed_page": 1, "lessons": []},
        {"number": "2", "title": "Play with Numbers", "printed_page": 2, "lessons": []},
        {"number": "3", "title": "Fractions", "printed_page": 9, "lessons": []},
    ]
    texts[2] = "1 Find Shapes Around Us"
    texts[10] = "9 Fractions"
    placed = place(chapters, texts, offset=1, after=1)
    assert [c["pdf_pages"][0] for c in placed] == [3, 7, 11]


def test_a_drawn_heading_is_accepted_on_its_printed_page_number_but_marked():
    texts = ["cover", INDEX, "1 Find Shapes Around Us", "2 shapes", "3 counting pictures", "4 more"]
    chapters = [
        {"number": "1", "title": "Find Shapes Around Us", "printed_page": 1, "lessons": []},
        {"number": "2", "title": "Play with Numbers", "printed_page": 3, "lessons": []},
    ]
    placed = place(chapters, texts, offset=1, after=1)
    assert [c["pdf_pages"][0] for c in placed] == [3, 5]
    assert [c["title_checked"] for c in placed] == [True, False]


def test_rows_without_a_number_are_page_furniture():
    from ncert_build.split import _is_chapter

    assert not _is_chapter({"number": "", "title": "Sr. No.", "printed_page": 1})
    assert _is_chapter({"number": "4", "title": "Fractions", "printed_page": 37})


def test_a_contents_page_printed_in_columns_pairs_titles_and_pages_in_order():
    from ncert_build.split import columns

    page = "INDEX\n Sr. No.   Chapter Name   Page No.\nMy Village\nWhere is a cock?\nThe number after, the number before \nand the middle number\n1\n2 \n3 \n1\n2 \n9 \n"
    assert [(c["title"], c["printed_page"]) for c in columns(page)] == [
        ("My Village", 1),
        ("Where is a cock?", 2),
        ("The number after, the number before and the middle number", 9),
    ]
    assert columns(INDEX) is None
