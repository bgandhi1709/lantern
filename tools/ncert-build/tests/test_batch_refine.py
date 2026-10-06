import json

from ncert_build.batch_refine import assemble, chapter_input, request
from ncert_build.claude import Usage
from ncert_build.refine_check import check

BUNDLE = """# ssc1-english-01 · Class 1 English
stage: foundational · pages: 1–2
write to: /somewhere/refined/ssc1-english/ssc1-english-01.json

## Draft concepts (local model: hints only)
1. Greetings [p1]

## Chapter text
[p1]
My First Day
"""
LESSONS = [{"number": "1.1", "title": "My First Day", "printed_page": 1}]


def test_input_drops_the_draft_and_write_path_and_lists_a_units_lessons():
    text = chapter_input(BUNDLE, LESSONS)
    assert "Draft concepts" not in text and "write to:" not in text
    assert text.startswith("This chapter is an English Unit") and "1.1 My First Day (book page 1)" in text
    assert "[p1]\nMy First Day" in text


def test_request_carries_effort_and_schema():
    params = request("ssc1-english-01__low", "claude-sonnet-5-5", "low", "text")["params"]
    assert params["output_config"]["effort"] == "low"
    assert params["output_config"]["format"]["type"] == "json_schema"


def _qa(n):
    return [{"q": f"Question {i}?", "a": "A short answer."} for i in range(n)]


def test_assembled_unit_passes_check_with_ids_filled_in():
    reply = {
        "chapter_title": "Unit One",
        "concepts": [
            {"lesson": "1.1", "name": f"Idea {n}", "summary": "s", "how_taught": "h", "key_terms": [], "prerequisites": [], "pages": [1], "qa": _qa(5)}
            for n in range(10)
        ],
    }
    refined = assemble(reply, "ssc1-english-01", "claude-sonnet-5-5", LESSONS)
    assert refined["concepts"][9]["id"] == "ssc1-english-01-c10" and refined["board"] == "ssc"
    assert check(refined, "ssc1-english-01", 2) == []
    refined["concepts"][0]["lesson"] = "9.9"
    assert check(refined, "ssc1-english-01", 2) == ["concept 1: lesson must be one of the Unit's lessons"]
    assert json.dumps(refined)


def test_batch_cost_is_half_and_counts_cache_tokens():
    usage = Usage(input_tokens=1_000_000, output_tokens=100_000, cache_read_tokens=1_000_000)
    assert usage.cost_usd("claude-sonnet-5-5") == 2.0 + 1.0 + 0.2
    assert usage.cost_usd("claude-sonnet-5-5", batch=True) == (2.0 + 1.0 + 0.2) / 2


def test_a_lesson_echoed_with_its_title_keeps_only_the_number():
    reply = {"chapter_title": "Unit One", "concepts": [{"lesson": "1.1 My First Day", "name": "n", "qa": []}]}
    assert assemble(reply, "ssc1-english-01", "m", LESSONS)["concepts"][0]["lesson"] == "1.1"


def test_blanks_and_book_symbols_are_not_markdown_but_markdown_is():
    from ncert_build.refine_check import MARKDOWN

    for content in ["My name is ___.", "We had ___ there.", "Place 1, 2 and # in the rest.", "Start with @.", "In b_s, a bus"]:
        assert not MARKDOWN.search(content), content
    for markdown in ["This is **bold**.", "An *important* word.", "Use `code`.", "# Heading", "An _emphasised_ word."]:
        assert MARKDOWN.search(markdown), markdown


def test_concept_limit_scales_with_chapter_size():
    from ncert_build.refine_check import _concept_range

    assert _concept_range(unit=False, page_count=1) == (1, 8)
    assert _concept_range(unit=False, page_count=8) == (2, 8)
    assert _concept_range(unit=False, page_count=18) == (2, 12)
    assert _concept_range(unit=True, page_count=23) == (2, 15)
