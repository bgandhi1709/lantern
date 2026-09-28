from ncert_build.super_context import (
    ask_which_earlier_concepts,
    cap_to_budget,
    earlier_concepts,
    first_appearance_index,
    normalize_whitespace,
    quote_found,
    token_estimate,
)

REFINED = {
    "bejm101": {
        "concepts": [
            {"id": "bejm101-c1", "name": "Counting", "summary": "Numbers 1 to 20.", "key_terms": ["count", "number"]},
            {"id": "bejm101-c2", "name": "Shapes", "summary": "Circle and square.", "key_terms": ["circle", "square"]},
        ]
    },
    "bejm102": {
        "concepts": [
            {"id": "bejm102-c1", "name": "Addition", "summary": "Adding two small numbers.", "key_terms": ["add", "number"]},
        ]
    },
    # bejm103 not refined yet: must be skipped, not an error.
}


def test_token_estimate_is_chars_over_four():
    assert token_estimate("abcd" * 10) == 10


def test_normalize_whitespace_collapses_and_trims():
    assert normalize_whitespace("  a  \n b\t c  ") == "a b c"


def test_quote_found_matches_across_whitespace_differences():
    chapter = "The cat sat\non the mat."
    assert quote_found("cat sat on the mat", chapter)


def test_quote_found_is_exact_no_fuzzy_matching():
    chapter = "The cat sat on the mat."
    assert not quote_found("cat lay on the mat", chapter)


def test_quote_found_rejects_blank_quote():
    assert not quote_found("   ", "anything")


def test_earlier_concepts_flattens_in_chapter_order_and_skips_unrefined():
    concepts = earlier_concepts(["bejm101", "bejm103", "bejm102"], REFINED)
    assert [c["id"] for c in concepts] == ["bejm101-c1", "bejm101-c2", "bejm102-c1"]
    assert concepts[0] == {
        "chapter_id": "bejm101",
        "id": "bejm101-c1",
        "name": "Counting",
        "summary": "Numbers 1 to 20.",
        "key_terms": ["count", "number"],
    }


def test_earlier_concepts_defaults_missing_key_terms():
    refined = {"x01": {"concepts": [{"id": "x01-c1", "name": "N", "summary": "S"}]}}
    assert earlier_concepts(["x01"], refined)[0]["key_terms"] == []


def test_first_appearance_index_keeps_the_first_and_is_case_insensitive():
    concepts = earlier_concepts(["bejm101", "bejm102"], REFINED)
    index = first_appearance_index(concepts)
    assert index["number"]["id"] == "bejm101-c1"  # taught in Counting, not repeated in Addition
    assert index["NUMBER".lower()]["id"] == "bejm101-c1"
    assert index["add"]["id"] == "bejm102-c1"


def test_cap_to_budget_stops_once_the_fraction_is_exceeded():
    concepts = earlier_concepts(["bejm101", "bejm102"], REFINED)
    chapter_text = "x" * 40  # token_estimate == 10
    kept = cap_to_budget(concepts, chapter_text, fraction=0.5)  # budget == 5 tokens
    assert len(kept) == 1
    assert kept[0]["id"] == "bejm101-c1"


def test_cap_to_budget_always_keeps_at_least_the_first():
    concepts = earlier_concepts(["bejm101"], REFINED)
    kept = cap_to_budget(concepts, chapter_text="x", fraction=0.25)  # budget == 0 tokens
    assert len(kept) == 1


def test_cap_to_budget_empty_input_is_empty_output():
    assert cap_to_budget([], "some chapter text", fraction=0.25) == []


CANDIDATES = earlier_concepts(["bejm101", "bejm102"], REFINED)
CHAPTER_TEXT = "We add two numbers to make ten. Count on your fingers."


def test_ask_which_earlier_concepts_keeps_a_pick_with_a_real_quote():
    def fake_chat(system, user, schema):
        return {"picks": [{"earlier_concept_index": 2, "quote": "We add two numbers to make ten."}]}

    picks = ask_which_earlier_concepts(CHAPTER_TEXT, CANDIDATES, fake_chat, grade=2)
    assert [p["id"] for p in picks] == ["bejm102-c1"]
    assert picks[0]["quote"] == "We add two numbers to make ten."


def test_ask_which_earlier_concepts_drops_a_pick_whose_quote_is_not_real():
    def fake_chat(system, user, schema):
        return {"picks": [{"earlier_concept_index": 0, "quote": "invented line not in the passage"}]}

    assert ask_which_earlier_concepts(CHAPTER_TEXT, CANDIDATES, fake_chat, grade=2) == []


def test_ask_which_earlier_concepts_drops_out_of_range_and_duplicate_indices():
    def fake_chat(system, user, schema):
        return {
            "picks": [
                {"earlier_concept_index": 99, "quote": "We add two numbers to make ten."},
                {"earlier_concept_index": 2, "quote": "We add two numbers to make ten."},
                {"earlier_concept_index": 2, "quote": "Count on your fingers."},
            ]
        }

    picks = ask_which_earlier_concepts(CHAPTER_TEXT, CANDIDATES, fake_chat, grade=2)
    assert len(picks) == 1
    assert picks[0]["quote"] == "We add two numbers to make ten."  # the first hit for index 2 wins


def test_ask_which_earlier_concepts_with_no_candidates_never_calls_the_model():
    def fake_chat(system, user, schema):
        raise AssertionError("must not be called with an empty candidate list")

    assert ask_which_earlier_concepts(CHAPTER_TEXT, [], fake_chat, grade=2) == []
