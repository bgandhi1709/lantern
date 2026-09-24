from ncert_build.cli import parse_grades
from ncert_build.draft import CONSOLIDATE_SCHEMA, chunks, consolidate, draft_chapter, merge

CHAPTER = {
    "chapter_id": "jemh101",
    "grade": 10,
    "subject": "Mathematics",
    "book_title": "Mathematics",
    "title_guess": "REAL NUMBERS",
    "source_sha256": "abc",
    "sections": [
        {"kind": "section", "number": "1.1", "heading": "Introduction", "pages": [1], "text": "a" * 50},
        {"kind": "section", "number": "1.2", "heading": "Primes", "pages": [2, 3], "text": "b" * 50},
        {"kind": "exercise", "number": "1.1", "heading": "EXERCISE 1.1", "pages": [3], "text": "c" * 50},
    ],
}


def test_chunks_pack_sections_with_page_markers():
    packed = chunks(CHAPTER, max_chars=10_000)
    assert len(packed) == 1
    assert "[page 1]\n## Introduction" in packed[0] and "[page 2]\n## Primes" in packed[0]


def test_chunks_split_when_sections_do_not_fit():
    packed = chunks(CHAPTER, max_chars=90)
    assert len(packed) == 3
    assert all(len(p) <= 90 for p in packed)


def test_oversized_section_is_split_on_its_own():
    big = dict(CHAPTER, sections=[{"kind": "page", "number": "1", "heading": None, "pages": [1], "text": "x" * 250}])
    # 250 characters of text plus the 9-character "[page 1]\n" marker
    assert [len(p) for p in chunks(big, max_chars=100)] == [100, 100, 59]


def test_merge_joins_concepts_with_the_same_name():
    merged = merge(
        [
            {"chapter_title": "", "concepts": [
                {"name": "Prime factorisation", "summary": "s1", "key_terms": ["prime"], "pages": [2], "kid_questions": ["What is a prime?"]},
            ]},
            {"chapter_title": "Real Numbers", "concepts": [
                {"name": "prime  Factorisation", "summary": "s2", "key_terms": ["Prime", "factor"], "pages": [3, 2], "kid_questions": ["what is a prime?", "Why unique?"]},
                {"name": "Irrational numbers", "summary": "s3", "key_terms": [], "pages": [4], "kid_questions": []},
            ]},
        ]
    )
    assert merged["chapter_title"] == "Real Numbers"
    assert [c["name"] for c in merged["concepts"]] == ["Prime factorisation", "Irrational numbers"]
    prime = merged["concepts"][0]
    assert prime["summary"] == "s1"
    assert prime["key_terms"] == ["prime", "factor"]
    assert prime["pages"] == [2, 3]
    assert prime["kid_questions"] == ["What is a prime?", "Why unique?"]


def test_draft_chapter_records_how_it_was_made():
    calls = []

    def fake_chat(system, user, schema):
        calls.append((system, user))
        return {"chapter_title": "", "concepts": [
            {"name": "Primes", "summary": "s", "key_terms": [], "pages": [2], "kid_questions": ["q"]},
        ]}

    result = draft_chapter(CHAPTER, fake_chat, "qwen3:8b")
    assert len(calls) == 1
    assert "Class 10" in calls[0][0] and "Class 10 Mathematics" in calls[0][1]
    assert result["status"] == "draft"
    assert result["chapter_title"] == "REAL NUMBERS"  # falls back to the segmenter's guess
    assert result["meta"]["model"] == "qwen3:8b" and result["meta"]["prompt_version"] == "draft-v2"


def test_parse_grades():
    assert parse_grades("1-3,10") == {1, 2, 3, 10}
    assert parse_grades(None) is None


def concept(name, terms=(), pages=(), questions=()):
    return {"name": name, "summary": f"about {name}", "key_terms": list(terms), "pages": list(pages),
            "kid_questions": list(questions)}


DRAFTED = [
    concept("Positional words in poem", ["above"], [1], ["Where is the cat?"]),
    concept("Positional words in flag", ["top"], [6], ["Is saffron on top?"]),
    concept("Counting to 9", ["nine"], [8], ["What comes after 8?"]),
    concept("Positional words in train", ["before"], [7], ["Where is the cat?"]),
]


def test_consolidate_merges_groups_and_keeps_everything():
    def fake_chat(system, user, schema):
        assert schema is CONSOLIDATE_SCHEMA and "3. Positional words in train" in user
        return {"concepts": [
            {"name": "Positional words", "summary": "above, below, before", "members": [0, 1, 3]},
            {"name": "Counting to 9", "summary": "numbers", "members": [2]},
        ]}

    merged = consolidate(DRAFTED, fake_chat, grade=1)
    assert [c["name"] for c in merged] == ["Positional words", "Counting to 9"]
    positional = merged[0]
    assert positional["key_terms"] == ["above", "top", "before"]
    assert positional["pages"] == [1, 6, 7]
    assert positional["kid_questions"] == ["Where is the cat?", "Is saffron on top?"]
    assert positional["merged_from"] == ["Positional words in poem", "Positional words in flag", "Positional words in train"]
    assert merged[1]["merged_from"] == []


def test_consolidate_keeps_concepts_the_model_drops_or_repeats():
    def fake_chat(system, user, schema):
        return {"concepts": [
            {"name": "Positional words", "summary": "s", "members": [0, 1, 99]},
            {"name": "Again", "summary": "s", "members": [1]},  # already used: ignored
        ]}

    merged = consolidate(DRAFTED, fake_chat, grade=1)
    assert [c["name"] for c in merged] == ["Positional words", "Counting to 9", "Positional words in train"]


def test_short_lists_are_not_consolidated():
    def fail(*_):
        raise AssertionError("should not be called")

    assert consolidate(DRAFTED[:3], fail, grade=1) == DRAFTED[:3]
