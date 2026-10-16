"""The Board profile (board.json): how a Board's Books are built and shown (ADR-0014).

The expected files are written out by hand from the ADR, so a test cannot agree with a wrong profile.
"""

import json

from ncert_build import cbse, pipeline, ssc


def test_cbse_profile_names_the_board_orders_its_subjects_and_takes_titles_from_the_refined_files():
    assert cbse.PROFILE == {
        "board": "cbse",
        "name": "Central Board of Secondary Education",
        "publisher": "NCERT",
        "source": "https://ncert.nic.in/textbook.php",
        "medium": "English",
        "classes": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
        "guidance": [],
        "language_rule": "English medium only; Hindi and other-language Books are listed but not built",
        "subjects": [
            {"name": "Mathematics", "icon": "maths"},
            {"name": "English", "icon": "english"},
            {"name": "Science", "icon": "science"},
            {"name": "Social Science", "icon": "book"},
            {"name": "The World Around Us", "icon": "evs"},
            {"name": "Environmental Studies", "icon": "evs"},
        ],
        "chapter_titles": "refined",
        "profile_version": "cbse-v1",
    }


def test_ssc_profile_keeps_its_build_rules_and_gains_the_serving_ones():
    assert ssc.PROFILE == {
        "board": "ssc",
        "name": "Maharashtra State Board",
        "publisher": "Balbharati",
        "source": "https://books.ebalbharati.in/",
        "medium": "English",
        "classes": [1, 2, 3, 4, 5],
        "chapter_unit": {"English": "unit", "Mathematics": "lesson", "Environmental Studies": "lesson"},
        "lessons_nested_in_chapter": {"English": True},
        "guidance": ["front_matter", "beside_lesson"],
        "language_rule": "simple English; no language Subjects in scope",
        "subjects": [
            {"name": "Mathematics", "icon": "maths"},
            {"name": "English", "icon": "english"},
            {"name": "Environmental Studies", "icon": "evs"},
        ],
        "chapter_titles": "toc",
        "profile_version": "ssc-v2",
    }


def test_the_icons_are_a_closed_set_the_app_can_draw():
    for profile in (cbse.PROFILE, ssc.PROFILE):
        assert {subject["icon"] for subject in profile["subjects"]} <= {"maths", "english", "science", "evs", "book"}


def test_write_profile_puts_the_boards_own_board_json_in_the_data_directory(tmp_path):
    for board, expected in (("cbse", cbse.PROFILE), ("ssc", ssc.PROFILE)):
        layout = pipeline.Layout(tmp_path / board)

        pipeline.write_profile(layout, board)

        assert json.loads(layout.board_profile.read_text(encoding="utf-8")) == expected


def test_the_catalog_stage_writes_the_profile_for_cbse_too(tmp_path, monkeypatch):
    # NCERT's book list needs the network; a page with no books is enough to see the profile written beside the catalog.
    monkeypatch.setattr(pipeline.http, "fetch", lambda url: b"<html></html>")
    layout = pipeline.Layout(tmp_path)

    pipeline.refresh_catalog(layout, "cbse")

    assert json.loads(layout.board_profile.read_text(encoding="utf-8"))["profile_version"] == "cbse-v1"
