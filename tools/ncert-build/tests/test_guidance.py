from ncert_build.guidance import HEADING, chapter_at, extract

FRONT = """Instructions for Teachers
Read each poem aloud first and let the children join in. Encourage them to act out the actions."""
LESSON = """3
For Teachers : Using pictures, videos, the internet, or other media, show students 3D shapes found in the
surroundings. Point out the shapes present in the surroundings.
Observe the table below and match the objects with their shapes."""
TOC = {"chapters": [{"chapter_id": "ssc3-maths-01", "pdf_pages": [3, 4]}]}


def test_finds_guidance_headings():
    assert HEADING.search(FRONT) and HEADING.search(LESSON)
    assert HEADING.search("For Teachers and Parents") and HEADING.search("Guidelines for Teachers")
    assert not HEADING.search("Observe the table below and match the objects.")


def test_front_matter_notes_belong_to_the_book_and_lesson_notes_to_their_chapter():
    replies = {
        FRONT: {"notes": [{"heading": "Instructions for Teachers", "text": "Read each poem aloud first and let the children join in. Encourage them to act out the actions."}]},
        LESSON: {"notes": [{"heading": "For Teachers", "text": "Using pictures, videos, the internet, or other media, show students 3D shapes found in the\nsurroundings."}]},
    }
    document, rejected = extract("ssc3-maths", "Mathematics Standard 3", [FRONT, "cover", LESSON], TOC, lambda s, u, schema: replies[u], "qwen3:8b")
    assert [n["pages"] for n in document["book_notes"]] == [[1]]
    assert [(n["chapter_id"], n["page"]) for n in document["chapter_notes"]] == [("ssc3-maths-01", 3)]
    assert rejected == []


def test_a_note_the_model_rewrote_is_rejected():
    reply = {"notes": [{"heading": "For Teachers", "text": "Show the class some 3D shapes from everyday life using media."}]}
    document, rejected = extract("ssc3-maths", "Maths 3", [LESSON], TOC, lambda s, u, schema: reply, "qwen3:8b")
    assert document["chapter_notes"] == [] and document["book_notes"] == [] and len(rejected) == 1


def test_chapter_at_maps_pdf_pages():
    assert chapter_at(4, TOC) == "ssc3-maths-01" and chapter_at(9, TOC) is None
