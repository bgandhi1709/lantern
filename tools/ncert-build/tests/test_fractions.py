import pypdfium2 as pdfium

from ncert_build.fractions import ascii_digits, page_bars, pair, patch


def pdf_with(content: bytes, path):
    """A one-page PDF drawing ``content`` in Helvetica: enough geometry for fractions to read."""
    objects = [
        b"<< /Type /Catalog /Pages 2 0 R >>",
        b"<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 200] /Contents 4 0 R "
        b"/Resources << /Font << /F1 5 0 R >> >> >>",
        b"<< /Length %d >>\nstream\n" % len(content) + content + b"\nendstream",
        b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
    ]
    out, offsets = bytearray(b"%PDF-1.4\n"), []
    for number, body in enumerate(objects, start=1):
        offsets.append(len(out))
        out += b"%d 0 obj\n" % number + body + b"\nendobj\n"
    xref = len(out)
    out += b"xref\n0 %d\n0000000000 65535 f \n" % (len(objects) + 1)
    out += b"".join(b"%010d 00000 n \n" % o for o in offsets)
    out += b"trailer\n<< /Size %d /Root 1 0 R >>\nstartxref\n%d\n%%%%EOF\n" % (len(objects) + 1, xref)
    path.write_bytes(bytes(out))
    return path


# "Take 3/4 of it" with the fraction stacked: 3 above a bar, 4 below, and a lone dash elsewhere.
STACKED = b"""BT /F1 10 Tf 20 100 Td (Take) Tj ET
BT /F1 10 Tf 50 106 Td (3) Tj ET
0 0 0 RG 1 w 49 103 m 57 103 l S
BT /F1 10 Tf 50 94 Td (4) Tj ET
BT /F1 10 Tf 62 100 Td (of it) Tj ET
200 150 m 210 150 l S"""


def test_a_number_over_a_bar_over_a_number_is_a_fraction(tmp_path):
    document = pdfium.PdfDocument(pdf_with(STACKED, tmp_path / "f.pdf"))
    page = document[0]
    assert pair(page.get_textpage(), page_bars(page)) == [("3", "4")]


def test_fractions_are_joined_in_reading_order():
    text, joined = patch("Take 3\n4 of 1\n2 and 3\n4", [("3", "4"), ("3", "4")])
    assert (text, joined) == ("Take 3/4 of 1\n2 and 3/4", 2)


def test_expressions_are_bracketed_and_spacing_is_forgiven():
    text, _ = patch("= (5 ×  7)\n(12 × 18) and a × c\nb × d", [("(5×7)", "(12×18)"), ("a×c", "b×d")])
    assert text == "= (5×7)/(12×18) and (a×c)/(b×d)"


def test_a_fraction_missing_from_the_text_is_skipped_not_forced():
    assert patch("nothing stacked here", [("1", "2")]) == ("nothing stacked here", 0)


def test_a_number_inside_a_longer_one_is_not_a_numerator():
    assert patch("13\n4", [("3", "4")])[1] == 0


def test_maths_bold_digits_read_as_plain_digits():
    assert ascii_digits("𝟏 × 𝟒") == "1 × 4"
