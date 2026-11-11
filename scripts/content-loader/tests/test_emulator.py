import socket
import sys
import uuid
from pathlib import Path

import pytest
import urllib3

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
sys.path.insert(0, str(Path(__file__).resolve().parent))

import load_board as loader
from boards import make_ncert, make_ssc, manifest, write, catalog_entry


def emulator_is_up() -> bool:
    try:
        socket.create_connection(("localhost", 8081), timeout=1).close()
        return True
    except OSError:
        return False


pytestmark = pytest.mark.skipif(not emulator_is_up(), reason="the Cosmos DB emulator is not running (docker compose up cosmos)")


@pytest.fixture
def container():
    from azure.cosmos import CosmosClient, PartitionKey

    urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)
    client = CosmosClient(loader.EMULATOR_ENDPOINT, credential=loader.EMULATOR_KEY, connection_verify=False)
    database = client.create_database_if_not_exists("loader-tests")
    made = database.create_container_if_not_exists(id=f"content-{uuid.uuid4().hex[:8]}", partition_key=PartitionKey("/classKey"))
    yield made
    database.delete_container(made)


def stored(container):
    return sorted(
        (item["classKey"], item["id"], item["release"] if "release" in item else None)
        for item in container.query_items("SELECT c.classKey, c.id, c.release FROM c", enable_cross_partition_query=True)
    )


def run(root, container, board="ncert"):
    lines = []
    code = loader.load_board(loader.DirSource(root), loader.CosmosStore(container, sleep=lambda seconds: None), board, True, out=lines.append)
    return code, lines


def test_a_board_is_written_to_cosmos_and_a_second_run_leaves_the_same_documents(tmp_path, container):
    make_ncert(tmp_path / "ncert")

    code, _ = run(tmp_path / "ncert", container)
    first = stored(container)
    run(tmp_path / "ncert", container)

    assert code == 0
    assert len(first) == 1 + 9 + 4
    assert stored(container) == first
    assert {key for key, _, _ in first} == {"cbse", "cbse-3", "cbse-4"}


def test_documents_the_board_no_longer_holds_are_removed_from_cosmos(tmp_path, container):
    root = tmp_path / "ncert"
    make_ncert(root)
    run(root, container)
    for folder in ("refined/cma1/cma102.json", "summaries/cma1/cma102.json", "plans/cma1/cma102.plan.json"):
        (root / folder).unlink()
    write(root, "manifest/class-3.json", manifest(3, {"cma1": ["cma101"], "cma2": ["cma201"]}))
    (root / "manifest/class-4.json").unlink()
    write(root, "catalog.json", [catalog_entry("cma1", 3), catalog_entry("cma2", 3)])

    run(root, container)

    ids = {id_ for _, id_, _ in stored(container)}
    assert "chapter:cma102" not in ids and "plan:cma102" not in ids
    assert "class:cbse-4" not in ids and "chapter:cmb101" not in ids
    assert "chapter:cma101" in ids


def test_an_ssc_board_is_written_to_cosmos(tmp_path, container):
    make_ssc(tmp_path / "ssc")

    code, _ = run(tmp_path / "ssc", container, board="ssc")

    assert code == 0
    assert {key for key, _, _ in stored(container)} == {"ssc", "ssc-3"}
