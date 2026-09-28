from ncert_build.bundle import render as render_v1
from ncert_build.bundle_v2 import SUPER_CONTEXT_HEADING, render

CHAPTER = {
    "chapter_id": "bejm102",
    "grade": 2,
    "subject": "Mathematics",
    "book_title": "Joyful Mathematics",
    "sections": [
        {"kind": "section", "number": "1", "heading": "Shapes", "pages": [4], "text": "It has 6 faces so we need 6 stars."},
    ],
}

EARLIER_CONTEXT = [
    {
        "chapter_id": "bejm101",
        "id": "bejm101-c1",
        "name": "Counting in Groups",
        "summary": "Counting objects by making equal groups instead of one by one.",
        "key_terms": ["count", "group"],
        "quote": "It has 6 faces so we need 6 stars.",
    }
]


def test_chapter_text_onward_matches_v1_byte_for_byte():
    v2 = render(CHAPTER, None, "out.json", EARLIER_CONTEXT)
    v1 = render_v1(CHAPTER, None, "out.json")
    assert v2.split("## Chapter text")[1] == v1.split("## Chapter text")[1]


def test_super_context_section_lists_the_earlier_concept_and_its_quote():
    v2 = render(CHAPTER, None, "out.json", EARLIER_CONTEXT)
    assert SUPER_CONTEXT_HEADING in v2
    assert "bejm101 · Counting in Groups: Counting objects by making equal groups instead of one by one." in v2
    assert 'used here: "It has 6 faces so we need 6 stars."' in v2
    assert v2.index(SUPER_CONTEXT_HEADING) < v2.index("## Chapter text")


def test_no_earlier_context_still_gets_a_section_saying_so():
    v2 = render(CHAPTER, None, "out.json", [])
    assert f"{SUPER_CONTEXT_HEADING}\n(none found)" in v2


def test_write_creates_parent_directories(tmp_path):
    from ncert_build.bundle_v2 import write

    target = tmp_path / "bundles-v2" / "bejm1" / "bejm102.md"
    write("hello", target)
    assert target.read_text(encoding="utf-8") == "hello"
