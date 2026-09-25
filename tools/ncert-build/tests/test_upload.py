from ncert_build.upload import commands


def test_uploads_each_stage_folder_and_the_catalogue(tmp_path):
    for folder in ("pdf", "drafts"):
        (tmp_path / folder).mkdir()
    (tmp_path / "catalog.json").write_text("[]")

    built = commands(tmp_path, "stlanterndevabcde")

    assert [c[3] for c in built] == ["upload-batch", "upload-batch", "upload"]
    pdf, drafts, catalog = built
    assert pdf[pdf.index("--destination-path") + 1] == "pdf"
    assert pdf[pdf.index("--overwrite") + 1] == "false"  # PDFs never change
    assert drafts[drafts.index("--overwrite") + 1] == "true"  # drafts are redone with new prompts
    assert all(c[c.index("--auth-mode") + 1] == "login" for c in built)  # storage keys are off
    assert catalog[catalog.index("--container-name") + 1] == "ncert"


def test_missing_stages_are_skipped(tmp_path):
    assert commands(tmp_path, "account") == []
