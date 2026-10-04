from ncert_build.catalog import book_id_of
from ncert_build.ssc import classify, parse_listing

# Trimmed from an ebalbharati async postback: the shape of one listed book.
LISTING = """
<div id='div2' class='bookDetails1' runat='server' ><div id='divImages' class='divImage' runat='server'> <div style='cursor:pointer'; onclick='openpdf(303060004)'> <img id=imgedit_303060004 runat=server src=BookCovers/303060004.jpg/></div></div> <div id='DivTitle' class='diveTitle' runat='server'><div class=divbooknm title='३ री गणित इंग्रजी'>३ री गणित इंग्रजी</div></div><div id='btnAddtoCart' runat='server' class='addtocart_small'><div class='button' onclick='SaveToDisk("https://ebooks.ebalbharati.in/pdfs/303060004.pdf","303060004.pdf");'>Download&nbsp;</div></div>  </div>
<div id='div2' class='bookDetails1' runat='server' ><div id='divImages' class='divImage' runat='server'> <div style='cursor:pointer'; onclick='openpdf(303000866)'> <img id=imgedit_303000866 runat=server src=BookCovers/303000866.jpg/></div></div> <div id='DivTitle' class='diveTitle' runat='server'><div class=divbooknm title='३ री शिकू मराठी आनंदाने'>३ री शिकू मराठी आनंदाने</div></div></div>
"""


def test_reads_item_ids_and_titles():
    assert parse_listing(LISTING) == [("303060004", "३ री गणित इंग्रजी"), ("303000866", "३ री शिकू मराठी आनंदाने")]


def test_keeps_english_maths_and_evs_only():
    maths = classify("303060004", "३ री गणित इंग्रजी", 3)
    assert (maths.book_id, maths.subject, maths.board) == ("ssc3-maths", "Mathematics", "ssc")
    assert maths.pdf_url == "https://ebooks.ebalbharati.in/pdfs/303060004.pdf"
    assert classify("303060001", "३ री बालभारती इंग्रजी", 3).book_id == "ssc3-english"
    assert classify("303000866", "३ री शिकू मराठी आनंदाने", 3) is None
    assert classify("303000871", "३ री कला शिक्षण इंग्रजी", 3) is None


def test_evs_parts_and_only_the_mumbai_district_edition():
    part2 = classify("303000869", "३ री आपल्या सभोवतालचे जग भा २ इंग्रजी", 3)
    assert (part2.book_id, part2.part, part2.title) == ("ssc3-evs2", 2, "The World Around Us Part 2")
    mumbai = classify("303030544", "३ री जि. मुंबई आ.स.ज. भा-१ इंग्रजी", 3)
    assert (mumbai.book_id, mumbai.edition) == ("ssc3-evs1", "2026, Mumbai district")
    assert classify("303030552", "३ री जि. पुणे आ.स.ज. भा-१ इंग्रजी", 3) is None
    assert classify("503000541", "५ वी परिसर अभ्यास भाग-१ इंग्रजी", 5).title == "Environmental Studies Part 1"


def test_ssc_chapter_ids_are_readable_and_map_back_to_their_book():
    maths = classify("303060004", "३ री गणित इंग्रजी", 3)
    from dataclasses import replace

    ids = replace(maths, last_chapter=14).chapter_ids()
    assert (ids[0], ids[-1]) == ("ssc3-maths-01", "ssc3-maths-14")
    assert book_id_of("ssc3-maths-14") == "ssc3-maths"
    assert book_id_of("aejm113") == "aejm1"
