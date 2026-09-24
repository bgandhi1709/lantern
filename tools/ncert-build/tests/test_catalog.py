from ncert_build.catalog import parse_textbook_page, select

# Trimmed from ncert.nic.in/textbook.php: the shape of the real dropdown script.
PAGE = """
   if((document.test.tclass.value==1) && (document.test.tsubject.options[sind].text=="English"))
		document.test.tbook.options[0].text="..Select Book Title..";
		//document.test.tbook.options[1].text="Marigold";
		document.test.tbook.options[1].text="Mridang";
		document.test.tbook.options[1].value="textbook.php?aemr1=0-9"
		//document.test.tbook.options[1].value="textbook.php?aeen1=0-10"
	else if((document.test.tclass.value==1) && (document.test.tsubject.options[sind].text=="Mathematics"))
		document.test.tbook.options[1].text="Joyful-Mathematics (English)";
		document.test.tbook.options[1].value="textbook.php?aejm1=0-13"
		document.test.tbook.options[2].text="Joyful-Mathematics (Gujarati)";
		document.test.tbook.options[2].value="textbook.php?agjm1=0-13"
	else if((document.test.tclass.value==8) && (document.test.tsubject.options[sind].text=="Social Science"))
		document.test.tbook.options[1].text="Our-Pasts-III  ";
		document.test.tbook.options[1].value="textbook.php?hess2=1-8"
	else if((document.test.tclass.value==1) && (document.test.tsubject.options[sind].text=="Hindi"))
		document.test.tbook.options[1].text="Sarangi";
		document.test.tbook.options[1].value="textbook.php?ahsr1=0-19"
"""


def by_id(books):
    return {b.book_id: b for b in books}


def test_reads_every_live_book_and_skips_commented_ones():
    books = by_id(parse_textbook_page(PAGE))
    assert set(books) == {"aemr1", "aejm1", "agjm1", "hess2", "ahsr1"}


def test_value_gives_chapter_range_and_front_matter():
    maths = by_id(parse_textbook_page(PAGE))["aejm1"]
    assert (maths.grade, maths.subject, maths.first_chapter, maths.last_chapter) == (1, "Mathematics", 1, 13)
    assert maths.has_front_matter
    assert maths.chapter_ids()[0] == "aejm101" and maths.chapter_ids()[-1] == "aejm113"
    assert maths.front_matter_id == "aejm1ps"


def test_range_starting_at_one_has_no_front_matter():
    pasts = by_id(parse_textbook_page(PAGE))["hess2"]
    assert not pasts.has_front_matter
    assert pasts.title == "Our-Pasts-III"
    assert len(pasts.chapter_ids()) == 8


def test_medium_comes_from_title_or_code_letter():
    books = by_id(parse_textbook_page(PAGE))
    assert books["aejm1"].medium == "English"  # "(English)" in the title
    assert books["agjm1"].medium == "Gujarati"
    assert books["aemr1"].medium == "English"  # code letter e
    assert books["ahsr1"].medium == "Hindi"  # code letter h


def test_select_filters_combine():
    books = parse_textbook_page(PAGE)
    chosen = select(books, grades={1}, subjects={"Mathematics"}, mediums={"English"})
    assert [b.book_id for b in chosen] == ["aejm1"]
