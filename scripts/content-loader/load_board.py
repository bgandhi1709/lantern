#!/usr/bin/env python3
import argparse
import hashlib
import json
import re
import sys
import time
from dataclasses import dataclass, field
from pathlib import Path

CLASS_MANIFEST_SCHEMA = "class-manifest-v1"
EMULATOR_ENDPOINT = "https://localhost:8081"
EMULATOR_KEY = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
BOOK_ID = re.compile(r"^[A-Za-z0-9._-]+$")
CLASS_MANIFEST = re.compile(r"^manifest/class-(\d+)\.json$")
THROTTLE_ATTEMPTS = 12
THROTTLE_PAUSE_CAP_SECONDS = 5.0


@dataclass(frozen=True)
class BoardRules:
    key: str
    skips_unbuilt_books: bool
    requires_board_file: bool
    book_folders: tuple[str, ...]


RULES = {
    "ncert": BoardRules(key="cbse", skips_unbuilt_books=True, requires_board_file=False, book_folders=()),
    "ssc": BoardRules(key="ssc", skips_unbuilt_books=False, requires_board_file=True, book_folders=("toc", "guidance")),
}


class SourceError(Exception):
    pass


@dataclass
class ClassLoad:
    class_key: str
    documents: list[dict]
    unknown_files: list[str] = field(default_factory=list)

    @property
    def release(self) -> str:
        return self.documents[-1]["release"]

    def count(self, kind: str) -> int:
        return sum(1 for document in self.documents if document["kind"] == kind)


class DirSource:
    def __init__(self, root: Path):
        self.root = Path(root)

    def names(self, prefix: str) -> list[str]:
        base = self.root / prefix
        if not base.is_dir():
            return []
        return sorted(path.relative_to(self.root).as_posix() for path in base.rglob("*") if path.is_file())

    def read(self, name: str) -> bytes:
        return (self.root / name).read_bytes()


class BlobSource:
    def __init__(self, container):
        self.container = container

    def names(self, prefix: str) -> list[str]:
        return sorted(self.container.list_blob_names(name_starts_with=prefix))

    def read(self, name: str) -> bytes:
        from azure.core.exceptions import ResourceNotFoundError

        try:
            return self.container.download_blob(name).readall()
        except ResourceNotFoundError as error:
            raise FileNotFoundError(name) from error


class MemoryStore:
    def __init__(self):
        self.documents: dict[tuple[str, str], dict] = {}
        self.upserts: list[dict] = []

    def upsert(self, document: dict) -> None:
        self.upserts.append(document)
        self.documents[(document["classKey"], document["id"])] = document

    def stale_ids(self, class_key: str, release: str) -> list[str]:
        return [id_ for (key, id_), document in self.documents.items() if key == class_key and document["release"] != release]

    def all_ids(self, class_key: str) -> list[str]:
        return [id_ for (key, id_) in self.documents if key == class_key]

    def class_keys(self, board_key: str) -> list[str]:
        return [key for (key, id_) in self.documents if id_.startswith("class:") and key.startswith(f"{board_key}-")]

    def delete(self, class_key: str, id_: str) -> None:
        del self.documents[(class_key, id_)]


class CosmosStore:
    def __init__(self, container, sleep=time.sleep):
        self.container = container
        self.sleep = sleep

    def upsert(self, document: dict) -> None:
        throttled(lambda: self.container.upsert_item(document), self.sleep)

    def stale_ids(self, class_key: str, release: str) -> list[str]:
        return self.ids("SELECT c.id FROM c WHERE c.release != @release", class_key, [{"name": "@release", "value": release}])

    def all_ids(self, class_key: str) -> list[str]:
        return self.ids("SELECT c.id FROM c", class_key, [])

    def ids(self, query: str, class_key: str, parameters: list[dict]) -> list[str]:
        found = throttled(lambda: list(self.container.query_items(query, parameters=parameters, partition_key=class_key)), self.sleep)
        return [item["id"] for item in found]

    def class_keys(self, board_key: str) -> list[str]:
        query = "SELECT VALUE c.classKey FROM c WHERE c.kind = 'class' AND c.board = @board"
        parameters = [{"name": "@board", "value": board_key}]
        return throttled(lambda: list(self.container.query_items(query, parameters=parameters, enable_cross_partition_query=True)), self.sleep)

    def delete(self, class_key: str, id_: str) -> None:
        throttled(lambda: self.container.delete_item(id_, partition_key=class_key), self.sleep)


def throttled(call, sleep=time.sleep):
    from azure.cosmos.exceptions import CosmosHttpResponseError

    for attempt in range(THROTTLE_ATTEMPTS):
        try:
            return call()
        except CosmosHttpResponseError as error:
            if error.status_code != 429 or attempt == THROTTLE_ATTEMPTS - 1:
                raise
            sleep(pause_seconds(error, attempt))


def pause_seconds(error, attempt: int) -> float:
    headers = getattr(error.response, "headers", None) or {}
    asked = headers.get("x-ms-retry-after-ms")
    pause = float(asked) / 1000 if asked else 0.1 * 2**attempt
    return min(pause, THROTTLE_PAUSE_CAP_SECONDS)


def read_json(source, name: str):
    try:
        return json.loads(source.read(name))
    except FileNotFoundError as error:
        raise SourceError(f"{name} is missing") from error


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SourceError(message)


def require_id(value, what: str) -> str:
    require(isinstance(value, str) and BOOK_ID.match(value) is not None, f"{what} {value!r} is not a usable id")
    return value


class Inventory:
    def __init__(self, source):
        self.catalog = read_json(source, "catalog.json")
        self.refined = self.by_book(source, "refined")
        self.summaries = self.by_book(source, "summaries")
        self.plans = self.by_book(source, "plans")
        self.manifests = sorted(int(match.group(1)) for name in source.names("manifest/") if (match := CLASS_MANIFEST.match(name)))
        self.unknown_manifest_files = [name for name in source.names("manifest/") if not CLASS_MANIFEST.match(name)]
        listed = {entry.get("book_id") for entry in self.catalog}
        self.unlisted_files = [
            f"{folder}/{book}/{name}"
            for folder, books in (("refined", self.refined), ("summaries", self.summaries))
            for book, names in books.items()
            if book not in listed
            for name in names
        ]

    @staticmethod
    def by_book(source, folder: str) -> dict[str, list[str]]:
        books: dict[str, list[str]] = {}
        for name in source.names(f"{folder}/"):
            book, _, rest = name[len(folder) + 1 :].partition("/")
            books.setdefault(book, []).append(rest)
        return books


def is_file_of(names: list[str], suffix: str) -> list[str]:
    return sorted(name for name in names if "/" not in name and name.endswith(suffix))


def unknown_files(folder: str, book: str, names: list[str]) -> list[str]:
    return [f"{folder}/{book}/{name}" for name in names if "/" in name or not name.endswith(".json")]


def built_chapters(rules: BoardRules, inventory: Inventory, book: str, class_number: int) -> list[str]:
    chapters = is_file_of(inventory.refined.get(book, []), ".json")
    if not chapters:
        require(rules.skips_unbuilt_books, f"Book {book} is listed for Class {class_number} but has no refined content")
        return []
    return [name[: -len(".json")] for name in chapters]


def build_class(source, rules: BoardRules, inventory: Inventory, class_number: int) -> ClassLoad:
    manifest_name = f"manifest/class-{class_number}.json"
    manifest = read_json(source, manifest_name)
    require(manifest.get("schema") == CLASS_MANIFEST_SCHEMA, f"{manifest_name} has schema {manifest.get('schema')!r}")
    require(manifest.get("class") == class_number, f"{manifest_name} is for Class {manifest.get('class')}")
    chapters_of_manifest = manifest_chapters(manifest, manifest_name)

    class_key = f"{rules.key}-{class_number}"
    documents: list[dict] = []
    unknown: list[str] = []
    listed = [entry for entry in inventory.catalog if entry.get("grade") == class_number]
    built_ids: set[str] = set()

    for entry in listed:
        book = require_id(entry.get("book_id"), "A catalog book_id")
        chapter_ids = built_chapters(rules, inventory, book, class_number)
        if not chapter_ids:
            continue
        book_document = {"catalog": entry, "chapterIds": chapter_ids}
        for folder in rules.book_folders:
            book_document[folder] = read_json(source, f"{folder}/{book}.json")
        documents.append(entry_document("book", class_key, book, book=book, **book_document))
        for chapter in chapter_ids:
            require_id(chapter, "A chapter id")
            built_ids.add(chapter)
            documents.extend(chapter_documents(source, inventory, class_key, book, chapter, chapters_of_manifest))
        unknown += unknown_files("refined", book, inventory.refined.get(book, []))
        unknown += unknown_files("summaries", book, inventory.summaries.get(book, []))

    require(built_ids, f"Class {class_number} has no built Book")
    not_built = sorted(set(chapters_of_manifest) - built_ids)
    require(not not_built, f"{manifest_name} lists chapters that are not built: {', '.join(not_built)}")
    not_listed = sorted(built_ids - set(chapters_of_manifest))
    require(not not_listed, f"{manifest_name} does not list built chapters: {', '.join(not_listed)}")

    class_document = entry_document(
        "class",
        class_key,
        class_key,
        board=rules.key,
        number=class_number,
        manifest=manifest,
        counts={
            "books": sum(1 for d in documents if d["kind"] == "book"),
            "chapters": len(built_ids),
            "plans": len(built_ids),
        },
    )
    return finish(class_key, documents, class_document, unknown)


def manifest_chapters(manifest: dict, manifest_name: str) -> dict[str, dict]:
    found: dict[str, dict] = {}
    for subject in manifest.get("subjects", []):
        chapters = subject.get("chapters", [])
        require(subject.get("chapterCount") == len(chapters), f"{manifest_name}: Subject {subject.get('subjectId')} counts {subject.get('chapterCount')} chapters and lists {len(chapters)}")
        for chapter in chapters:
            chapter_id = require_id(chapter.get("chapterId"), "A manifest chapterId")
            require_id(chapter.get("bookId"), "A manifest bookId")
            require(chapter_id not in found, f"{manifest_name} lists chapter {chapter_id} twice")
            found[chapter_id] = {**chapter, "subjectId": subject.get("subjectId")}
    return found


def chapter_documents(source, inventory: Inventory, class_key: str, book: str, chapter: str, in_manifest: dict[str, dict]) -> list[dict]:
    summary_name = f"summaries/{book}/{chapter}.json"
    plan_name = f"plans/{book}/{chapter}.plan.json"
    require(f"{chapter}.json" in inventory.summaries.get(book, []), f"Book {book} has refined content with no summary: {summary_name}")
    require(f"{chapter}.plan.json" in inventory.plans.get(book, []), f"Book {book} has refined content with no Plan: {plan_name}")
    listed = in_manifest.get(chapter)
    require(listed is None or listed.get("bookId") == book, f"Chapter {chapter} is in Book {book} but the manifest says Book {listed and listed.get('bookId')}")
    refined = read_json(source, f"refined/{book}/{chapter}.json")
    summary = read_json(source, summary_name)
    plan = read_json(source, plan_name)
    return [
        entry_document("chapter", class_key, chapter, book=book, chapter=chapter, refined=refined, summary=summary),
        entry_document("plan", class_key, chapter, book=book, chapter=chapter, plan=plan),
    ]


def entry_document(kind: str, class_key: str, id_: str, **fields) -> dict:
    return {"id": f"{kind}:{id_}", "kind": kind, "classKey": class_key, **fields}


def finish(class_key: str, documents: list[dict], class_document: dict, unknown: list[str]) -> ClassLoad:
    ordered = sorted(documents, key=lambda document: document["id"]) + [class_document]
    release = hashlib.sha256(json.dumps(ordered, sort_keys=True).encode()).hexdigest()[:16]
    return ClassLoad(class_key, [{**document, "release": release} for document in ordered], sorted(set(unknown)))


def board_document(source, rules: BoardRules) -> dict | None:
    try:
        profile = json.loads(source.read("board.json"))
    except FileNotFoundError:
        require(not rules.requires_board_file, "board.json is missing")
        return None
    return entry_document("board", rules.key, rules.key, board=rules.key, profile=profile)


def write_class(store, load: ClassLoad) -> None:
    for document in load.documents:
        store.upsert(document)
    for id_ in store.stale_ids(load.class_key, load.release):
        store.delete(load.class_key, id_)


def remove_classes_no_longer_held(store, rules: BoardRules, held: set[str]) -> list[str]:
    gone = [key for key in store.class_keys(rules.key) if key not in held]
    for class_key in gone:
        for id_ in store.all_ids(class_key):
            store.delete(class_key, id_)
    return gone


def load_board(source, store, board: str, apply: bool, out=print) -> int:
    rules = RULES[board]
    inventory = Inventory(source)
    profile = board_document(source, rules)
    loads: list[ClassLoad] = []
    failures: list[str] = []

    for class_number in inventory.manifests:
        try:
            loads.append(build_class(source, rules, inventory, class_number))
        except SourceError as error:
            failures.append(f"Class {rules.key}-{class_number} is not ready: {error}")

    for name in inventory.unknown_manifest_files + inventory.unlisted_files:
        out(f"unknown file, ignored: {name}")
    for load in loads:
        for name in load.unknown_files:
            out(f"unknown file, ignored: {name}")
        out(f"{load.class_key}: {len(load.documents)} documents ({load.count('book')} books, {load.count('chapter')} chapters, {load.count('plan')} plans), release {load.release}")
    for failure in failures:
        out(f"ERROR {failure}")
    out(f"{board}: {sum(len(load.documents) for load in loads) + (profile is not None)} documents in {len(loads)} classes, {len(failures)} failed")

    if apply:
        if profile is not None:
            store.upsert(profile)
        for load in loads:
            write_class(store, load)
        held = {load.class_key for load in loads} | {f"{rules.key}-{number}" for number in inventory.manifests}
        for class_key in remove_classes_no_longer_held(store, rules, held):
            out(f"removed {class_key}, the Board no longer holds it")
    else:
        out("dry run: nothing written, pass --apply to write")
    return 1 if failures else 0


def open_source(args):
    if args.source_dir:
        return DirSource(Path(args.source_dir))
    from azure.storage.blob import ContainerClient

    container_name = args.blob_container or args.board
    if args.blob_connection_string:
        return BlobSource(ContainerClient.from_connection_string(args.blob_connection_string, container_name))
    from azure.identity import DefaultAzureCredential

    return BlobSource(ContainerClient(f"https://{args.account}.blob.core.windows.net", container_name, credential=DefaultAzureCredential()))


def open_store(args):
    from azure.cosmos import CosmosClient, PartitionKey

    if args.emulator:
        import urllib3

        urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)
        client = CosmosClient(EMULATOR_ENDPOINT, credential=EMULATOR_KEY, connection_verify=False)
        database = client.create_database_if_not_exists(args.cosmos_database)
        return CosmosStore(database.create_container_if_not_exists(id=args.cosmos_container, partition_key=PartitionKey("/classKey")))
    from azure.identity import DefaultAzureCredential

    client = CosmosClient(args.cosmos_endpoint, credential=DefaultAzureCredential())
    return CosmosStore(client.get_database_client(args.cosmos_database).get_container_client(args.cosmos_container))


def parse(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Moves a Board's content from its Blob container into Cosmos DB. A dry run unless --apply.")
    parser.add_argument("--board", required=True, choices=sorted(RULES))
    where = parser.add_mutually_exclusive_group(required=True)
    where.add_argument("--source-dir", help="a local copy of the Board's container")
    where.add_argument("--account", help="storage account; signs in with az login")
    where.add_argument("--blob-connection-string", help="for Azurite")
    parser.add_argument("--blob-container", help="defaults to the Board name")
    target = parser.add_mutually_exclusive_group()
    target.add_argument("--emulator", action="store_true", help="the local Cosmos DB emulator; creates the database and container")
    target.add_argument("--cosmos-endpoint", help="the Cosmos DB account; signs in with az login")
    parser.add_argument("--cosmos-database", default="lantern")
    parser.add_argument("--cosmos-container", default="content")
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args(argv)
    if args.apply and not (args.emulator or args.cosmos_endpoint):
        parser.error("--apply needs --emulator or --cosmos-endpoint")
    return args


def main(argv: list[str]) -> int:
    args = parse(argv)
    store = open_store(args) if args.apply else None
    try:
        return load_board(open_source(args), store, args.board, args.apply)
    except SourceError as error:
        print(f"ERROR {error}")
        return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
