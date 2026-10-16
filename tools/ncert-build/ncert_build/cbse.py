"""The CBSE Board (NCERT): its Board profile, the counterpart of ``ssc.PROFILE``.

The profile is the Board's rules in one file, ``board.json``. The manifest stage reads it to order the Subjects, pick their
icons and find each Chapter's title; nothing in the API reads it (ADR-0014). Unlike SSC's, it holds no rules for cutting a
Book into Chapters: NCERT's chapter PDFs are already one per Chapter.
"""

from __future__ import annotations

PROFILE = {
    "board": "cbse",
    "name": "Central Board of Secondary Education",
    "publisher": "NCERT",
    "source": "https://ncert.nic.in/textbook.php",
    "medium": "English",
    "classes": list(range(1, 11)),
    "guidance": [],
    "language_rule": "English medium only; Hindi and other-language Books are listed but not built",
    # The display order of the Subjects, and the closed set of icons the app draws. A Subject not listed here sorts last,
    # by name, with the icon "book" (Social Science has no drawing yet).
    "subjects": [
        {"name": "Mathematics", "icon": "maths"},
        {"name": "English", "icon": "english"},
        {"name": "Science", "icon": "science"},
        {"name": "Social Science", "icon": "book"},
        {"name": "The World Around Us", "icon": "evs"},
        {"name": "Environmental Studies", "icon": "evs"},
    ],
    # NCERT keeps a Chapter's title only in the refined file.
    "chapter_titles": "refined",
    "profile_version": "cbse-v1",
}
