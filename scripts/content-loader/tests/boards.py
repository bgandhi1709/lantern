import json
from pathlib import Path


def write(root: Path, name: str, content) -> None:
    path = root / name
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content if isinstance(content, str) else json.dumps(content))


def add_book(root: Path, book: str, chapters: list[str], *, summaries=True, plans=True, toc_and_guidance=False) -> None:
    for chapter in chapters:
        write(root, f"refined/{book}/{chapter}.json", {"chapter_id": chapter, "concepts": [{"id": f"{chapter}-c1"}]})
        if summaries:
            write(root, f"summaries/{book}/{chapter}.json", {"chapter_id": chapter, "summary": f"About {chapter}."})
        if plans:
            write(root, f"plans/{book}/{chapter}.plan.json", {"concepts": [{"concept_id": f"{chapter}-c1", "video": "video.mp4"}], "check_pool": []})
            write(root, f"plans/{book}/{chapter}/{chapter}-c1/video.mp4", "video")
    if toc_and_guidance:
        write(root, f"toc/{book}.json", {"book": book, "toc": []})
        write(root, f"guidance/{book}.json", {"book": book, "guidance": "Teach slowly."})


def manifest(class_number: int, books: dict[str, list[str]], **overrides) -> dict:
    chapters = [{"chapterId": chapter, "bookId": book, "number": index + 1, "title": chapter, "summary": ""} for book, ids in books.items() for index, chapter in enumerate(ids)]
    made = {
        "schema": "class-manifest-v1",
        "board": "cbse",
        "class": class_number,
        "subjects": [{"subjectId": "maths", "name": "Maths", "icon": "maths", "chapterCount": len(chapters), "chapters": chapters}],
    }
    return {**made, **overrides}


def catalog_entry(book: str, grade: int) -> dict:
    return {"book_id": book, "grade": grade, "title": book, "subject": "Maths"}


def make_ncert(root: Path, *, extra_catalog=()) -> dict[str, list[str]]:
    books = {"cma1": ["cma101", "cma102"], "cma2": ["cma201"]}
    write(root, "catalog.json", [catalog_entry("cma1", 3), catalog_entry("cma2", 3), catalog_entry("cunbuilt1", 3), catalog_entry("cmb1", 4), *extra_catalog])
    write(root, "board.json", {"board": "cbse", "name": "CBSE"})
    for book, chapters in books.items():
        add_book(root, book, chapters)
    write(root, "manifest/class-3.json", manifest(3, books))
    other = {"cmb1": ["cmb101"]}
    add_book(root, "cmb1", other["cmb1"])
    write(root, "manifest/class-4.json", manifest(4, other))
    return books


def make_ssc(root: Path) -> dict[str, list[str]]:
    books = {"ssc3-maths1": ["sm101", "sm102"]}
    write(root, "catalog.json", [catalog_entry("ssc3-maths1", 3)])
    write(root, "board.json", {"board": "ssc", "name": "Maharashtra SSC"})
    add_book(root, "ssc3-maths1", books["ssc3-maths1"], toc_and_guidance=True)
    write(root, "manifest/class-3.json", manifest(3, books, board="ssc"))
    return books
