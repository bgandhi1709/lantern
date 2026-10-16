"""The Class manifest stage (ADR-0014): one unified file per Board and Class, made from the Board profile and the build output.

Each test builds a small data directory by hand and compares the file with JSON written out by hand. The titles are invented.
"""

import json
from pathlib import Path

import pytest

from ncert_build import catalog, manifest, pipeline
from ncert_build.catalog import Book


def _layout(tmp_path, board="cbse"):
    layout = pipeline.Layout(tmp_path)
    pipeline.write_profile(layout, board)
    return layout


def _book(book_id, grade, subject, board="cbse", part=None, first=1, last=1):
    return Book(
        book_id=book_id, grade=grade, subject=subject, title=f"{subject} {book_id}", medium="English",
        first_chapter=first, last_chapter=last, has_front_matter=False, board=board, part=part,
    )


def _listed(layout, *books):
    catalog.save(list(books), layout.catalog)


def _write(path, document):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(document), encoding="utf-8")


def _built(layout, book_id, *chapters):
    """Builds the refined file and the summary of each (chapter id, title); a summary is 'About <id>.'"""
    for chapter_id, title in chapters:
        _write(layout.refined(book_id, chapter_id), {"chapter_id": chapter_id, "chapter_title": title, "concepts": []})
        _write(layout.root / "summaries" / book_id / f"{chapter_id}.json", {"chapter_id": chapter_id, "summary": f"About {chapter_id}."})


def _make(layout, grades):
    """Runs the stage for the Board the layout's profile names."""
    return manifest.write_all(layout, json.loads(layout.board_profile.read_text(encoding="utf-8"))["board"], grades)


def _read(layout, grade):
    return json.loads(layout.manifest(grade).read_text(encoding="utf-8"))


def _subject(manifest_file, subject_id):
    return next(subject for subject in manifest_file["subjects"] if subject["subjectId"] == subject_id)


def test_one_built_book_makes_one_subject_with_its_chapters(tmp_path):
    layout = _layout(tmp_path)
    _listed(layout, _book("cesa1", 3, "English", last=2))
    _built(layout, "cesa1", ("cesa101", "Fun with Friends"), ("cesa102", "Second Chapter"))

    report = _make(layout, {3})

    assert report.done == 1 and not report.failed
    assert _read(layout, 3) == {
        "schema": "class-manifest-v1",
        "board": "cbse",
        "profileVersion": "cbse-v1",
        "class": 3,
        "subjects": [
            {
                "subjectId": "english",
                "name": "English",
                "icon": "english",
                "chapterCount": 2,
                "chapters": [
                    {"chapterId": "cesa101", "bookId": "cesa1", "number": 1, "title": "Fun with Friends", "summary": "About cesa101."},
                    {"chapterId": "cesa102", "bookId": "cesa1", "number": 2, "title": "Second Chapter", "summary": "About cesa102."},
                ],
            }
        ],
    }


def test_two_books_of_one_subject_are_one_subject_whose_count_is_all_their_chapters(tmp_path):
    # NCERT Class 7 Mathematics has two Books, each numbering its own Chapters from 1.
    layout = _layout(tmp_path)
    _listed(layout, _book("gegp2", 7, "Mathematics", last=1), _book("gegp1", 7, "Mathematics", last=2))
    _built(layout, "gegp1", ("gegp101", "One"), ("gegp102", "Two"))
    _built(layout, "gegp2", ("gegp201", "Three"))

    _make(layout, {7})

    maths = _subject(_read(layout, 7), "mathematics")
    assert maths["chapterCount"] == 3
    assert [(c["bookId"], c["number"], c["title"]) for c in maths["chapters"]] == [("gegp1", 1, "One"), ("gegp1", 2, "Two"), ("gegp2", 1, "Three")]


def test_the_count_is_the_chapters_built_not_the_range_the_catalog_declares(tmp_path):
    # NCERT jewe2 declares 9 Chapters and 7 are built.
    layout = _layout(tmp_path)
    _listed(layout, _book("jewe2", 10, "English", first=1, last=9))
    _built(layout, "jewe2", *[(f"jewe20{n}", f"Chapter {n}") for n in (1, 2, 3, 5, 6, 7, 8)])

    _make(layout, {10})

    english = _subject(_read(layout, 10), "english")
    assert english["chapterCount"] == 7
    assert [c["number"] for c in english["chapters"]] == [1, 2, 3, 5, 6, 7, 8]


def test_subjects_follow_the_profiles_order_and_an_unlisted_one_goes_last_by_name_with_the_book_icon(tmp_path):
    layout = _layout(tmp_path)
    _listed(
        layout,
        _book("zz1", 6, "Zoology Club"), _book("aa1", 6, "Art"), _book("sc1", 6, "Science"),
        _book("en1", 6, "English"), _book("ma1", 6, "Mathematics"), _book("ss1", 6, "Social Science"),
    )
    for book_id in ("zz1", "aa1", "sc1", "en1", "ma1", "ss1"):
        _built(layout, book_id, (f"{book_id}01", "Only"))

    _make(layout, {6})

    got = [(s["name"], s["icon"]) for s in _read(layout, 6)["subjects"]]
    assert got == [
        ("Mathematics", "maths"), ("English", "english"), ("Science", "science"), ("Social Science", "book"),
        ("Art", "book"), ("Zoology Club", "book"),
    ]


def test_both_evs_type_subjects_of_ncert_class_four_are_listed_each_under_its_own_name(tmp_path):
    layout = _layout(tmp_path)
    _listed(layout, _book("deap1", 4, "Environmental Studies"), _book("deev1", 4, "The World Around Us"))
    _built(layout, "deap1", ("deap101", "Looking"))
    _built(layout, "deev1", ("deev101", "Wondering"))

    _make(layout, {4})

    assert [(s["subjectId"], s["icon"]) for s in _read(layout, 4)["subjects"]] == [
        ("the-world-around-us", "evs"),
        ("environmental-studies", "evs"),
    ]


def test_ssc_takes_chapter_titles_from_the_toc_not_the_refined_file_and_numbers_from_the_chapter_id(tmp_path):
    # SSC's refined title is the longer "Unit One (Lessons 1.1 to 1.10)"; the toc has the title the Parent should see. The toc's
    # printed number is a label ("1", but "s1" in Class 3 Maths and "No. 2" in Class 5 EVS), so the number is the id's: -01 is 1.
    layout = _layout(tmp_path, "ssc")
    _listed(layout, _book("ssc3-english", 3, "English", board="ssc", last=2))
    _built(layout, "ssc3-english", ("ssc3-english-01", "Unit One (Lessons 1.1 to 1.10)"), ("ssc3-english-02", "Unit Two (Lessons 2.1 to 2.9)"))
    _write(
        layout.toc("ssc3-english"),
        {"book_id": "ssc3-english", "chapters": [
            {"chapter_id": "ssc3-english-01", "number": "s1", "title": "Unit One"},
            {"chapter_id": "ssc3-english-02", "number": "No. 2", "title": "Unit Two"},
        ]},
    )

    _make(layout, {3})

    english = _subject(_read(layout, 3), "english")
    assert _read(layout, 3)["profileVersion"] == "ssc-v2"
    assert [(c["chapterId"], c["number"], c["title"]) for c in english["chapters"]] == [
        ("ssc3-english-01", 1, "Unit One"), ("ssc3-english-02", 2, "Unit Two"),
    ]


def test_two_parts_of_an_ssc_subject_are_one_subject_in_part_order(tmp_path):
    layout = _layout(tmp_path, "ssc")
    _listed(layout, _book("ssc3-evs2", 3, "Environmental Studies", board="ssc", part=2), _book("ssc3-evs1", 3, "Environmental Studies", board="ssc", part=1))
    for part, title in ((1, "Air"), (2, "Soil")):
        book_id = f"ssc3-evs{part}"
        _built(layout, book_id, (f"{book_id}-01", f"{title} (long)"))
        _write(layout.toc(book_id), {"book_id": book_id, "chapters": [{"chapter_id": f"{book_id}-01", "number": "1", "title": title}]})

    _make(layout, {3})

    evs = _subject(_read(layout, 3), "environmental-studies")
    assert evs["chapterCount"] == 2
    assert [(c["bookId"], c["title"]) for c in evs["chapters"]] == [("ssc3-evs1", "Air"), ("ssc3-evs2", "Soil")]


@pytest.mark.parametrize("what", ["title", "summary"])
def test_a_chapter_with_no_title_or_summary_fails_its_class_naming_the_chapter_and_writes_nothing_for_it(tmp_path, capsys, what):
    layout = _layout(tmp_path)
    _listed(layout, _book("cesa1", 3, "English", last=2), _book("cemm1", 4, "Mathematics"))
    _built(layout, "cesa1", ("cesa101", "Fine"), ("cesa102", "Broken"))
    _built(layout, "cemm1", ("cemm101", "Counting"))
    if what == "title":
        _write(layout.refined("cesa1", "cesa102"), {"chapter_id": "cesa102", "chapter_title": "  ", "concepts": []})
    else:
        _write(layout.root / "summaries" / "cesa1" / "cesa102.json", {"chapter_id": "cesa102", "summary": ""})

    report = _make(layout, {3, 4})

    assert report.failed == ["class-3"]
    assert "cesa102" in capsys.readouterr().out
    assert not layout.manifest(3).exists()
    assert layout.manifest(4).exists()  # another Class is not held back


def test_a_chapter_file_that_is_not_named_after_its_book_fails_the_class(tmp_path, capsys):
    layout = _layout(tmp_path)
    _listed(layout, _book("cesa1", 3, "English"))
    _built(layout, "cesa1", ("stray", "Not a Chapter of cesa1"))

    report = _make(layout, {3})

    assert report.failed == ["class-3"] and "stray" in capsys.readouterr().out
    assert not layout.manifest(3).exists()


def test_a_book_ncert_lists_but_has_not_built_is_ignored_and_a_class_with_no_built_book_gets_no_file(tmp_path):
    layout = _layout(tmp_path)
    _listed(layout, _book("cesa1", 3, "English"), _book("chin1", 3, "Hindi"), _book("cxx1", 9, "Science"))
    _built(layout, "cesa1", ("cesa101", "One"))

    report = _make(layout, {3, 9})

    assert [s["subjectId"] for s in _read(layout, 3)["subjects"]] == ["english"]  # Hindi has nothing refined
    assert not layout.manifest(9).exists()
    assert report.missing == ["class-9"]


def test_an_ssc_book_that_is_listed_but_has_nothing_built_is_a_broken_master_not_a_skip(tmp_path, capsys):
    layout = _layout(tmp_path, "ssc")
    _listed(layout, _book("ssc3-maths", 3, "Mathematics", board="ssc"))

    report = _make(layout, {3})

    assert report.failed == ["class-3"] and "ssc3-maths" in capsys.readouterr().out
    assert not layout.manifest(3).exists()


def test_only_the_selected_classes_are_written(tmp_path):
    layout = _layout(tmp_path)
    _listed(layout, _book("cesa1", 3, "English"), _book("desa1", 4, "English"))
    _built(layout, "cesa1", ("cesa101", "One"))
    _built(layout, "desa1", ("desa101", "One"))

    _make(layout, {4})

    assert not layout.manifest(3).exists() and layout.manifest(4).exists()


def test_the_stage_writes_the_current_profile_first_so_restored_data_with_an_old_board_json_is_not_trusted(tmp_path):
    # Data restored from UAT carries SSC's ssc-v1 board.json, and NCERT has none: the manifests must follow the code's profile.
    layout = pipeline.Layout(tmp_path)
    _write(layout.board_profile, {"board": "ssc", "profile_version": "ssc-v1", "classes": [3]})
    _listed(layout, _book("ssc3-maths", 3, "Mathematics", board="ssc"))
    _built(layout, "ssc3-maths", ("ssc3-maths-01", "Long title"))
    _write(layout.toc("ssc3-maths"), {"chapters": [{"chapter_id": "ssc3-maths-01", "number": "1", "title": "Short"}]})

    manifest.write_all(layout, "ssc", {3})

    assert json.loads(layout.board_profile.read_text(encoding="utf-8"))["profile_version"] == "ssc-v2"
    assert _read(layout, 3)["profileVersion"] == "ssc-v2"


def test_the_stage_needs_the_catalog_the_catalog_stage_writes(tmp_path):
    # The same message every stage gives (catalog.load): the catalog is what says which Books there are.
    with pytest.raises(SystemExit, match="ncert-build catalog"):
        manifest.write_all(pipeline.Layout(tmp_path), "cbse", {3})


def test_the_manifest_command_writes_the_classes_asked_for_and_exits_zero(tmp_path, monkeypatch, capsys):
    from ncert_build import cli

    layout = _layout(tmp_path)
    _listed(layout, _book("cesa1", 3, "English"), _book("desa1", 4, "English"))
    _built(layout, "cesa1", ("cesa101", "One"))
    _built(layout, "desa1", ("desa101", "One"))
    monkeypatch.setenv("LANTERN_DATA_DIR", str(tmp_path))

    code = cli.main(["--board", "cbse", "manifest", "--grades", "3"])

    assert code == 0
    assert layout.manifest(3).exists() and not layout.manifest(4).exists()
    assert "manifest: 1 done" in capsys.readouterr().out


def test_the_manifest_command_exits_non_zero_when_a_class_fails_so_a_script_notices(tmp_path, monkeypatch):
    from ncert_build import cli

    layout = _layout(tmp_path)
    _listed(layout, _book("cesa1", 3, "English"))
    _built(layout, "cesa1", ("cesa101", ""))
    monkeypatch.setenv("LANTERN_DATA_DIR", str(tmp_path))

    assert cli.main(["--board", "cbse", "manifest", "--grades", "3"]) == 1


def test_the_sample_class_manifest_in_the_docs_is_what_the_stage_writes_and_the_api_reads(tmp_path):
    # docs/architecture/contracts/class-manifest.cbse-7.json is the contract between this stage and the API's ClassManifest
    # reader, which parses the very same file in its own tests. Invented titles (ADR-0009). To change the format, change both.
    sample = Path(__file__).resolve().parents[3] / "docs" / "architecture" / "contracts" / "class-manifest.cbse-7.json"
    layout = _layout(tmp_path)
    _listed(layout, _book("gegp2", 7, "Mathematics"), _book("gegp1", 7, "Mathematics", last=2), _book("gepr1", 7, "English", last=2), _book("gaa1", 7, "Art"))
    _built(layout, "gegp1", ("gegp101", "Integers"), ("gegp102", "Fractions"))
    _built(layout, "gegp2", ("gegp201", "Lines"))
    _built(layout, "gepr1", ("gepr101", "A Poem"), ("gepr102", "A Story"))
    _built(layout, "gaa1", ("gaa101", "Colours"))

    _make(layout, {7})

    assert layout.manifest(7).read_text(encoding="utf-8") == sample.read_text(encoding="utf-8")
