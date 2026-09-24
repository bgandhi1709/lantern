import copy

from ncert_build.bundle import render, stage
from ncert_build.refine_check import check

CHAPTER = {
    "chapter_id": "aejm104",
    "grade": 1,
    "subject": "Mathematics",
    "book_title": "Joyful-Mathematics (English)",
    "sections": [
        {"kind": "page", "number": "1", "heading": None, "pages": [1], "text": "Count the ladybugs.\n"},
        {"kind": "page", "number": "2", "heading": None, "pages": [2], "text": "Make 10 with beads.\n"},
    ],
}
DRAFT = {
    "chapter_title": "Making 10",
    "concepts": [
        {"name": "Dot patterns", "summary": "See numbers without counting.", "pages": [1],
         "kid_questions": ["q1", "q2", "q3", "q4"]},
    ],
}


def test_stages_follow_ncf_2023():
    assert [stage(g) for g in (1, 2, 3, 5, 6, 8, 9, 10)] == [
        "foundational", "foundational", "preparatory", "preparatory", "middle", "middle", "secondary", "secondary",
    ]


def test_bundle_is_compact_markdown_with_hints_and_page_markers():
    text = render(CHAPTER, DRAFT, "/data/refined/aejm1/aejm104.json")
    assert text.startswith("# aejm104 · Class 1 Mathematics")
    assert "stage: foundational · pages: 1–2" in text
    assert "write to: /data/refined/aejm1/aejm104.json" in text
    assert "1. Dot patterns [p1]: See numbers without counting." in text
    assert "q: q1 / q2 / q3" in text and "q4" not in text  # hints are capped
    assert "[p2]\nMake 10 with beads." in text


def test_bundle_without_a_draft_still_has_the_text():
    assert "(no draft)" in render(CHAPTER, None, "out.json")


def qa(n):
    return [{"q": f"Why {i}?", "a": "Because we count one by one.", "try": "Count spoons."} for i in range(n)]


VALID = {
    "schema_version": "refine-v1",
    "chapter_id": "aejm104",
    "chapter_title": "Making 10",
    "model": "claude-sonnet-5",
    "concepts": [
        {"id": "aejm104-c1", "name": "Dot patterns", "summary": "Seeing small numbers at a glance.",
         "how_taught": "Ladybug dot cards, then fingers.", "key_terms": ["dots"], "prerequisites": [],
         "pages": [1], "qa": qa(5)},
        {"id": "aejm104-c2", "name": "Making 10", "summary": "Two groups that together make 10.",
         "how_taught": "Beads on a string.", "key_terms": ["ten"], "prerequisites": ["Counting to 9"],
         "pages": [2], "qa": qa(6)},
    ],
}


def test_a_valid_chapter_passes():
    assert check(VALID, "aejm104", page_count=2) == []


def test_contract_violations_are_named():
    broken = copy.deepcopy(VALID)
    broken["concepts"][1]["id"] = "c2"
    broken["concepts"][1]["pages"] = [9]
    broken["concepts"][0]["qa"] = qa(2)
    broken["concepts"][0]["qa"][0]["a"] = "word " * 61
    broken["concepts"][1]["qa"][0]["a"] = "**bold** answer"
    errors = check(broken, "aejm104", page_count=2)
    assert any("id must be aejm104-c2" in e for e in errors)
    assert any("pages must be chapter pages 1–2" in e for e in errors)
    assert any("2 qa; expected 5–8" in e for e in errors)
    assert any("a over 60 words" in e for e in errors)
    assert any("no markdown" in e for e in errors)


def test_wrong_chapter_and_duplicate_names_fail():
    broken = copy.deepcopy(VALID)
    broken["concepts"][1]["name"] = "dot patterns"
    errors = check(broken, "aejm105", page_count=2)
    assert any("chapter_id must be 'aejm105'" in e for e in errors)
    assert "concept names must be unique" in errors
