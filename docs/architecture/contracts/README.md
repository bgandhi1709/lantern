# Board content contract

The private `ncert` (CBSE) and `ssc` Blob containers hold each Board's Books, ready to read. They are made outside this
repository: extracting the syllabus is part of the separate `lantern-utilities` repository (ADR-0017). Lantern takes them
as given and reads them when it copies a Class into a Child's Content slice (ADR-0013) and when the one-off migration moves them into Cosmos DB (ADR-0020, D136). This file is the whole
contract; the file and folder names live in `ContentLayout`, and `ContentLayoutTests` pins them.

## Layout of a Board container

| Path | What | Read by the API |
|---|---|---|
| `catalog.json` | A JSON array with one object per Book | Yes |
| `refined/{book_id}/{chapter_id}.json` | One refined Chapter: its Concepts and Q&A | Copied unchanged |
| `summaries/{book_id}/{chapter_id}.json` | The Chapter summary; one for every refined file | Copied unchanged |
| `plans/{book_id}/{chapter_id}.plan.json` | One Plan for every refined Chapter | Copied unchanged |
| `plans/{book_id}/{chapter_id}/{concept_id}/video.mp4`, `poster.webp` | A Concept's video and poster | Copied unchanged |
| `manifest/class-{N}.json` | The Class manifest for Class N | Copied as the slice's `manifest.json` |
| `board.json` | The Board profile | SSC only (copied) |
| `toc/{book_id}.json` | A Book's contents list | SSC only (copied) |
| `guidance/{book_id}.json` | A Book's Book guidance | SSC only (copied) |

## `catalog.json`

An array of objects. Only two fields are read; every other field travels unchanged into the slice's `catalog.json`.

| Field | Type | Rule |
|---|---|---|
| `book_id` | string | Matches `^[A-Za-z0-9._-]+$`. Names the Book's folders under `refined/` and `summaries/` |
| `grade` | integer | The Class the Book belongs to. A Class takes the entries whose `grade` equals it |

The other fields in the real catalogs (for example `board`, `subject`, `title`, `medium`, `first_chapter`, `last_chapter`,
`part`, `edition`) are the publisher's listing, not a promise: the number of Chapters that exist is the number of
`summaries/` files, never `last_chapter`.

## Built and unbuilt Books

- A Book is built when `refined/{book_id}/` holds at least one file.
- CBSE: a listed Book that is not built is skipped.
- SSC: a listed Book that is not built is a broken master, and so is a missing `toc/`, `guidance/` or `board.json`.
- Both Boards: a refined file with no summary of the same name, or with no `plans/{book_id}/{chapter_id}.plan.json`, is a broken master (the error names the missing file), and a Class with no built Book cannot
  be copied.

## `manifest/class-{N}.json`

Schema id `class-manifest-v1`, camelCase, in the shape recorded in ADR-0014. The example
[`class-manifest.cbse-7.json`](class-manifest.cbse-7.json) is parsed by the API's own tests, and the content build keeps
a copy of it: change the format in both places. The file's `class` must equal N, or the copy fails.

## What a copy does with it

Everything is read and checked before the first write. The files above are copied byte for byte, the slice's
`catalog.json` keeps only the entries of the Class, and the slice records the version it was copied from. The master is read twice, and
a master that changed in between fails the copy, so upload content while no Child is being added.

## What the migration writes to Cosmos DB

The one-off migration (D136), a script the founder runs from their own machine and that is not in this repository, reads a Board's container and writes the `content` container of Cosmos DB (ADR-0020). It
applies the layout and the rules above a second time, because it is a one-off: a Board added later is loaded straight
into Cosmos DB. It names a file only through the paths in this contract, reads Books from the catalog and Chapters from the
Class manifest and never walks a folder, so a file it does not know (a stray `.idea/`) is reported and ignored.

Every document has `id` (`{kind}:{id}`), `kind` and `classKey` (the partition key: `cbse-3`, `ssc-5`; the Board document uses the
Board key, `cbse` or `ssc`). A Board's key is `cbse` for `ncert` and `ssc` for `ssc`.

| Kind | Id | Holds |
|---|---|---|
| board | `board:{board}` | `profile`: `board.json`. Required for SSC, optional for NCERT |
| class | `class:{classKey}` | `manifest` (the Class manifest as it is), `counts` (books, chapters, plans), `release` |
| book | `book:{book_id}` | `catalog`: the Book's catalog entry; `chapterIds`; for SSC `toc` and `guidance` |
| chapter | `chapter:{chapter_id}` | `refined` and `summary`: the two files as they are |
| plan | `plan:{chapter_id}` | `plan`: the Plan file as it is (`concepts`, `check_pool`) |

Every document of a Class carries the same `release`, a hash of everything the Class holds, so a second run over unchanged
content writes the same documents and a changed Class is told from the old one. A Class is written in this order: the
documents of its Books, Chapters and Plans, then the class document, then the deletion of every document with another
`release`. A run that fails before the class document leaves the previous one in place. A Class the Board no longer holds is
deleted whole.

A Class that breaks a rule is reported and not written, and the run ends with a failure; the other Classes are written.

- the manifest's `class` is the Class it is named for, its `schema` is known, each Subject's `chapterCount` equals its Chapters, and
  the Chapters it lists are exactly the Chapters built for the Class;
- a Book id and a chapter id match `^[A-Za-z0-9._-]+$` before they are used in a path;
- a refined Chapter with no summary or no Plan stops the Class, and the error names the missing file;
- an NCERT Book that is listed and not built is skipped; an SSC Book that is listed and not built, or one missing its `toc/` or
  `guidance/` file, stops the Class; a Class with no built Book stops;
- the video and poster of a Plan stay in Blob.
