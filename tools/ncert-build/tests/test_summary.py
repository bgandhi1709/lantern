import json

from ncert_build.summary import write

TEXT = "My Village is about the places in a village. Your child will learn to name the school, the well and the market, and to say what people do at each one."


def test_writes_a_valid_summary(tmp_path):
    target = tmp_path / "ssc1-maths" / "ssc1-maths-01.json"
    assert write(target, "ssc1-maths-01", "ssc", "claude-opus-5-5", TEXT) == "written"
    document = json.loads(target.read_text())
    assert document["schema_version"] == "summary-v1" and document["board"] == "ssc" and document["summary"] == TEXT


def test_never_writes_empty_short_or_markdown_text(tmp_path):
    target = tmp_path / "s.json"
    assert write(target, "c", "ssc", "m", "  ") == "empty, not written"
    assert write(target, "c", "ssc", "m", "Too short.").startswith("not written")
    assert write(target, "c", "ssc", "m", TEXT.replace("well", "**well**")) == "not written: markdown"
    assert not target.exists()


def test_keeps_an_existing_summary_unless_forced(tmp_path):
    target = tmp_path / "s.json"
    write(target, "c", "ssc", "m", TEXT)
    assert write(target, "c", "ssc", "m", TEXT + " More.") == "exists, kept"
    assert write(target, "c", "ssc", "m", TEXT + " More.", force=True) == "written"
