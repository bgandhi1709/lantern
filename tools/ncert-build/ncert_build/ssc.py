"""The SSC Board (Maharashtra State Board): its Board profile and its catalogue from ebalbharati.

ebalbharati has no API. Its book filter is an ASP.NET page whose UpdatePanel answers an async
postback (Text Books, a Standard, a Medium, a year) with the matching books: an item id and a
Marathi title such as ``३ री गणित इंग्रजी`` (Std 3 Maths, English medium). PDFs are then
``https://ebooks.ebalbharati.in/pdfs/<item id>.pdf``, one file per whole Book.
"""

from __future__ import annotations

import html
import http.cookiejar
import re
import urllib.parse
import urllib.request

from .catalog import Book
from .config import USER_AGENT

SITE = "https://books.ebalbharati.in/"
PDF_URL = "https://ebooks.ebalbharati.in/pdfs/{}.pdf"
YEAR = "2026"
GRADES = range(1, 6)
DISTRICT = "मुंबई"  # Std 3 EVS Part 1 is printed per district; D53 picks Mumbai

# Filter checkbox ids on the page.
_TEXT_BOOKS = "101"
_ENGLISH_MEDIUM = "303"

PROFILE = {
    "board": "ssc",
    "name": "Maharashtra State Board",
    "publisher": "Balbharati",
    "source": SITE,
    "medium": "English",
    "classes": list(GRADES),
    "chapter_unit": {"English": "unit", "Mathematics": "lesson", "Environmental Studies": "lesson"},
    "lessons_nested_in_chapter": {"English": True},
    "guidance": ["front_matter", "beside_lesson"],
    "language_rule": "simple English; no language Subjects in scope",
    "profile_version": "ssc-v1",
}

_BOOK = re.compile(r"openpdf\((\d+)\).*?DivTitle[^>]*>(.*?)</div>", re.S)
_DIGITS = str.maketrans("०१२३४५६७८९", "0123456789")
_PART = re.compile(r"(?:भा|भाग)\s*-?\s*(\d)")


def parse_listing(page: str) -> list[tuple[str, str]]:
    """(item id, title) for every book in a listing page or async postback response."""
    books = []
    for item_id, title in _BOOK.findall(page):
        books.append((item_id, " ".join(html.unescape(re.sub(r"<[^>]+>", " ", title)).split())))
    return books


def classify(item_id: str, title: str, grade: int) -> Book | None:
    """The in-scope Book for a listed title, or None: English Balbharati, Maths and EVS only."""
    title = title.translate(_DIGITS)
    if not title.endswith("इंग्रजी"):
        return None
    part_match = _PART.search(title)
    part = int(part_match.group(1)) if part_match else None
    edition = YEAR
    if "बालभारती" in title:
        subject, slug, name = "English", "english", f"English Balbharati Standard {grade}"
    elif "गणित" in title:
        subject, slug, name = "Mathematics", "maths", f"Mathematics Standard {grade}"
    elif "आ.स.ज." in title or "सभोवतालचे जग" in title or "परिसर अभ्यास" in title:
        if "जि." in title:
            if DISTRICT not in title:
                return None
            edition = f"{YEAR}, Mumbai district"
        if part is None:
            return None
        subject, slug = "Environmental Studies", f"evs{part}"
        name = ("Environmental Studies" if "परिसर अभ्यास" in title else "The World Around Us") + f" Part {part}"
    else:
        return None
    return Book(
        book_id=f"ssc{grade}-{slug}",
        grade=grade,
        subject=subject,
        title=name,
        medium="English",
        first_chapter=1,
        last_chapter=0,  # known once the Book is split
        has_front_matter=False,
        board="ssc",
        source_item_id=item_id,
        part=part,
        edition=edition,
        pdf_url=PDF_URL.format(item_id),
    )


class _Site:
    def __init__(self) -> None:
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        page = self._open(urllib.request.Request(SITE, headers={"User-Agent": USER_AGENT}))
        self.state = {
            name: html.unescape(value)
            for name, value in re.findall(r'name="(__[A-Z]+)" id="__[A-Z]+" value="([^"]*)"', page)
        }

    def _open(self, request: urllib.request.Request) -> str:
        with self.opener.open(request, timeout=60) as response:
            return response.read().decode("utf-8")

    def listing(self, selected: str) -> str:
        form = dict(self.state)
        form.update(
            {
                "ScriptManager1": "upBtn|upBtn",
                "__EVENTTARGET": "upBtn",
                "__EVENTARGUMENT": f"{selected}#1",
                "txtSelected": selected,
                "txtyear": YEAR,
                "__ASYNCPOST": "true",
            }
        )
        headers = {"User-Agent": USER_AGENT, "X-MicrosoftAjax": "Delta=true", "X-Requested-With": "XMLHttpRequest"}
        body = urllib.parse.urlencode(form).encode()
        return self._open(urllib.request.Request(SITE, data=body, headers=headers))


def fetch_catalog() -> list[Book]:
    site = _Site()
    books: dict[str, Book] = {}
    for grade in GRADES:
        page = site.listing(f"{_TEXT_BOOKS} {200 + grade} {_ENGLISH_MEDIUM}")
        for item_id, title in parse_listing(page):
            book = classify(item_id, title, grade)
            if book:
                books.setdefault(book.book_id, book)
    return sorted(books.values(), key=lambda b: (b.grade, b.subject, b.book_id))
