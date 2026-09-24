from ncert_build.extract import quality_flags

PROSE = "Plants make their food in the leaves using sunlight, water and air. " * 5


def test_prose_has_no_flags():
    assert quality_flags(PROSE) == []


def test_picture_pages_are_low_text():
    assert quality_flags("Let us Do\n9") == ["low_text"]


def test_arithmetic_pages_are_maths_dense():
    sums = "7 × 11 × 23 = 1771 and 3 × 7 × 11 × 23 = 5313 so 2 × 3 = 6 " * 6
    assert "maths_dense" in quality_flags(sums)


def test_private_use_characters_mean_garbled_fonts():
    assert "garbled" in quality_flags(PROSE + "\ue000" * 40)
