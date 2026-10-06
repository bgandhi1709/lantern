from ncert_build.upload import commands


def test_uploads_each_stage_folder_and_the_board_files(tmp_path):
    for folder in ("pdf", "pictures", "guidance"):
        (tmp_path / folder).mkdir()
    for name in ("catalog.json", "board.json"):
        (tmp_path / name).write_text("{}")

    built = commands(tmp_path, "lanternuat", "ssc")

    assert [c[3] for c in built] == ["upload-batch", "upload-batch", "upload", "upload"]
    pictures, guidance, catalog, board = built
    assert pictures[pictures.index("--destination-path") + 1] == "pictures"
    assert pictures[pictures.index("--pattern") + 1] == "*/*"  # the crops sit in a folder per chapter
    assert guidance[guidance.index("--pattern") + 1] == "*.json"  # one file per Book
    assert all(c[c.index("--auth-mode") + 1] == "login" for c in built)  # storage keys are off
    assert all(c[c.index("--overwrite") + 1] == "true" for c in built)
    assert pictures[pictures.index("--destination") + 1] == board[board.index("--container-name") + 1] == "ssc"
    assert not any("pdf" in c for c in built)  # the publisher's PDFs stay local


def test_missing_stages_are_skipped(tmp_path):
    assert commands(tmp_path, "account", "ncert") == []
