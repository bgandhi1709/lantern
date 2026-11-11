import json
import shutil
import sys
from pathlib import Path

import pytest
from azure.cosmos.exceptions import CosmosHttpResponseError

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
sys.path.insert(0, str(Path(__file__).resolve().parent))

import load_board as loader
from boards import add_book, catalog_entry, make_ncert, make_ssc, manifest, write


def run(root, store, board="ncert", apply=True):
    lines: list[str] = []
    code = loader.load_board(loader.DirSource(root), store, board, apply, out=lines.append)
    return code, lines


def ids_of(store, class_key):
    return sorted(id_ for (key, id_) in store.documents if key == class_key)


@pytest.fixture
def ncert(tmp_path):
    root = tmp_path / "ncert"
    make_ncert(root)
    return root


def test_a_board_is_loaded_as_board_class_book_chapter_and_plan_documents(ncert):
    store = loader.MemoryStore()

    code, _ = run(ncert, store)

    assert code == 0
    assert ids_of(store, "cbse") == ["board:cbse"]
    assert ids_of(store, "cbse-3") == [
        "book:cma1", "book:cma2", "chapter:cma101", "chapter:cma102", "chapter:cma201",
        "class:cbse-3", "plan:cma101", "plan:cma102", "plan:cma201",
    ]
    assert ids_of(store, "cbse-4") == ["book:cmb1", "chapter:cmb101", "class:cbse-4", "plan:cmb101"]
    assert store.documents[("cbse-3", "class:cbse-3")]["counts"] == {"books": 2, "chapters": 3, "plans": 3}


def test_a_chapter_carries_its_refined_content_and_summary_and_a_plan_is_stored_as_the_file_has_it(ncert):
    store = loader.MemoryStore()

    run(ncert, store)

    chapter = store.documents[("cbse-3", "chapter:cma101")]
    assert chapter["refined"]["concepts"] == [{"id": "cma101-c1"}]
    assert chapter["summary"]["summary"] == "About cma101."
    assert store.documents[("cbse-3", "plan:cma101")]["plan"] == json.loads((ncert / "plans/cma1/cma101.plan.json").read_text())


def test_the_class_document_is_written_last(ncert):
    store = loader.MemoryStore()

    run(ncert, store)

    class_three = [d["id"] for d in store.upserts if d["classKey"] == "cbse-3"]
    assert class_three[-1] == "class:cbse-3"


def test_a_dry_run_writes_nothing(ncert):
    store = loader.MemoryStore()

    code, lines = run(ncert, store, apply=False)

    assert code == 0
    assert store.upserts == []
    assert any("dry run" in line for line in lines)


def test_running_twice_leaves_the_same_documents(ncert):
    store = loader.MemoryStore()
    run(ncert, store)
    first = dict(store.documents)

    run(ncert, store)

    assert store.documents == first


def test_a_listed_ncert_book_that_is_not_built_is_skipped(ncert):
    store = loader.MemoryStore()

    run(ncert, store)

    assert "book:cunbuilt1" not in ids_of(store, "cbse-3")


def test_an_ssc_board_loads_with_each_books_contents_list_and_guidance(tmp_path):
    root = tmp_path / "ssc"
    make_ssc(root)
    store = loader.MemoryStore()

    code, _ = run(root, store, board="ssc")

    book = store.documents[("ssc-3", "book:ssc3-maths1")]
    assert code == 0
    assert book["toc"] == {"book": "ssc3-maths1", "toc": []}
    assert book["guidance"]["guidance"] == "Teach slowly."


def test_an_ssc_book_that_is_listed_and_not_built_fails_its_class(tmp_path):
    root = tmp_path / "ssc"
    make_ssc(root)
    write(root, "catalog.json", [catalog_entry("ssc3-maths1", 3), catalog_entry("ssc3-evs1", 3)])
    store = loader.MemoryStore()

    code, lines = run(root, store, board="ssc")

    assert code == 1
    assert any("ssc3-evs1" in line and "no refined content" in line for line in lines)
    assert ids_of(store, "ssc-3") == []


@pytest.mark.parametrize("folder", ["toc", "guidance"])
def test_an_ssc_book_missing_its_toc_or_guidance_fails_its_class_and_names_the_file(tmp_path, folder):
    root = tmp_path / "ssc"
    make_ssc(root)
    (root / folder / "ssc3-maths1.json").unlink()

    code, lines = run(root, loader.MemoryStore(), board="ssc")

    assert code == 1
    assert any(f"{folder}/ssc3-maths1.json is missing" in line for line in lines)


def test_an_ssc_board_without_board_json_fails(tmp_path):
    root = tmp_path / "ssc"
    make_ssc(root)
    (root / "board.json").unlink()

    with pytest.raises(loader.SourceError, match="board.json is missing"):
        run(root, loader.MemoryStore(), board="ssc")


def test_a_chapter_missing_its_summary_stops_the_class_and_names_the_file(ncert):
    (ncert / "summaries/cma1/cma102.json").unlink()
    store = loader.MemoryStore()

    code, lines = run(ncert, store)

    assert code == 1
    assert any("summaries/cma1/cma102.json" in line for line in lines)
    assert ids_of(store, "cbse-3") == []
    assert "class:cbse-4" in ids_of(store, "cbse-4")


def test_a_chapter_missing_its_plan_stops_the_class_and_names_the_file(ncert):
    (ncert / "plans/cma2/cma201.plan.json").unlink()

    code, lines = run(ncert, loader.MemoryStore())

    assert code == 1
    assert any("plans/cma2/cma201.plan.json" in line for line in lines)


def test_a_manifest_for_the_wrong_class_is_refused(ncert):
    write(ncert, "manifest/class-3.json", manifest(7, {"cma1": ["cma101", "cma102"], "cma2": ["cma201"]}))

    code, lines = run(ncert, loader.MemoryStore())

    assert code == 1
    assert any("is for Class 7" in line for line in lines)


def test_a_manifest_whose_counts_do_not_match_its_chapters_is_refused(ncert):
    broken = manifest(3, {"cma1": ["cma101", "cma102"], "cma2": ["cma201"]})
    broken["subjects"][0]["chapterCount"] = 5
    write(ncert, "manifest/class-3.json", broken)

    code, lines = run(ncert, loader.MemoryStore())

    assert code == 1
    assert any("counts 5 chapters and lists 3" in line for line in lines)


def test_a_manifest_that_lists_a_chapter_that_is_not_built_is_refused(ncert):
    write(ncert, "manifest/class-3.json", manifest(3, {"cma1": ["cma101", "cma102", "cma103"], "cma2": ["cma201"]}))

    code, lines = run(ncert, loader.MemoryStore())

    assert code == 1
    assert any("not built: cma103" in line for line in lines)


@pytest.mark.parametrize("bad", ["../secrets", "a/b", "", "book id"])
def test_a_book_id_is_never_used_as_a_path(ncert, bad):
    write(ncert, "catalog.json", [catalog_entry("cma1", 3), catalog_entry(bad, 3)])

    code, lines = run(ncert, loader.MemoryStore())

    assert code == 1
    assert any("is not a usable id" in line for line in lines)


def test_a_file_the_layout_does_not_know_is_reported_and_does_not_break_the_class(ncert):
    write(ncert, "manifest/.idea/.gitignore", "x")
    write(ncert, "refined/cma1/.idea/workspace.xml", "x")
    write(ncert, "refined/.idea/other.xml", "x")
    store = loader.MemoryStore()

    code, lines = run(ncert, store)

    assert code == 0
    assert "class:cbse-3" in ids_of(store, "cbse-3")
    reported = "\n".join(lines)
    assert "manifest/.idea/.gitignore" in reported
    assert "refined/cma1/.idea/workspace.xml" in reported
    assert "refined/.idea/other.xml" in reported


def test_a_document_the_board_no_longer_holds_is_removed(ncert):
    store = loader.MemoryStore()
    run(ncert, store)
    (ncert / "refined/cma1/cma102.json").unlink()
    (ncert / "summaries/cma1/cma102.json").unlink()
    (ncert / "plans/cma1/cma102.plan.json").unlink()
    write(ncert, "manifest/class-3.json", manifest(3, {"cma1": ["cma101"], "cma2": ["cma201"]}))

    run(ncert, store)

    assert "chapter:cma102" not in ids_of(store, "cbse-3")
    assert "plan:cma102" not in ids_of(store, "cbse-3")
    assert "chapter:cma101" in ids_of(store, "cbse-3")


def test_a_class_the_board_no_longer_holds_is_removed(ncert):
    store = loader.MemoryStore()
    run(ncert, store)
    (ncert / "manifest/class-4.json").unlink()
    write(ncert, "catalog.json", [catalog_entry("cma1", 3), catalog_entry("cma2", 3)])

    run(ncert, store)

    assert ids_of(store, "cbse-4") == []
    assert "class:cbse-3" in ids_of(store, "cbse-3")


def test_a_failed_run_leaves_the_previous_class_document_in_place(ncert):
    store = loader.MemoryStore()
    run(ncert, store)
    before = store.documents[("cbse-3", "class:cbse-3")]
    write(ncert, "summaries/cma1/cma101.json", {"chapter_id": "cma101", "summary": "Changed."})

    class Failing(loader.MemoryStore):
        def upsert(self, document):
            if document["id"] == "plan:cma201":
                raise RuntimeError("the connection dropped")
            super().upsert(document)

    failing = Failing()
    failing.documents = dict(store.documents)

    with pytest.raises(RuntimeError):
        run(ncert, failing)

    assert failing.documents[("cbse-3", "class:cbse-3")] == before


def test_changed_content_gets_a_new_release_and_the_old_release_is_removed(ncert):
    store = loader.MemoryStore()
    run(ncert, store)
    before = store.documents[("cbse-3", "class:cbse-3")]["release"]
    write(ncert, "summaries/cma1/cma101.json", {"chapter_id": "cma101", "summary": "Changed."})

    run(ncert, store)

    releases = {d["release"] for (key, _), d in store.documents.items() if key == "cbse-3"}
    assert len(releases) == 1 and before not in releases


class Throttle(CosmosHttpResponseError):
    def __init__(self):
        super().__init__(status_code=429, message="Request rate is large")


def test_a_throttled_call_waits_and_is_tried_again():
    calls: list[int] = []
    pauses: list[float] = []

    def call():
        calls.append(1)
        if len(calls) < 3:
            raise Throttle()
        return "written"

    assert loader.throttled(call, pauses.append) == "written"
    assert len(calls) == 3 and len(pauses) == 2


def test_a_call_that_keeps_being_throttled_gives_up():
    def call():
        raise Throttle()

    with pytest.raises(CosmosHttpResponseError):
        loader.throttled(call, lambda seconds: None)


def test_an_error_that_is_not_throttling_is_not_retried():
    calls: list[int] = []

    def call():
        calls.append(1)
        raise CosmosHttpResponseError(status_code=400, message="bad")

    with pytest.raises(CosmosHttpResponseError):
        loader.throttled(call, lambda seconds: None)
    assert len(calls) == 1


def test_apply_needs_a_cosmos_target(ncert):
    with pytest.raises(SystemExit):
        loader.parse(["--board", "ncert", "--source-dir", str(ncert), "--apply"])
