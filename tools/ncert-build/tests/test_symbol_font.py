from ncert_build.symbol_font import decode


def test_symbol_glyphs_become_maths_characters():
    # As pypdf returns Class 10 Maths, chapter 6: "Δ ABC ≅ Δ DPQ" and "∠ A = ∠ D"
    assert decode("\uf044 ABC \uf040\uf020\uf044 DPQ") == "Δ ABC ≅ Δ DPQ"
    assert decode("\uf0d0 A \uf03d \uf0d0 D") == "∠ A = ∠ D"


def test_greek_letters_and_operators():
    assert decode("\uf071 \uf061\uf062\uf067 \uf02d \uf0a3 \uf0d6") == "θ αβγ − ≤ √"


def test_tall_brackets_collapse_to_one_character():
    assert decode("\uf0e6\uf0e7\uf0e8x\uf0f6\uf0f7\uf0f8") == "(x)"


def test_ordinary_text_is_untouched():
    assert decode("sin A = 3/5") == "sin A = 3/5"
