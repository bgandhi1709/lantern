# Board content contract

The private `ncert` (CBSE) and `ssc` Blob containers hold each Board's Books, ready to read. They are made outside this
repository: extracting the syllabus is part of the separate `lantern-utilities` repository (ADR-0017). Lantern takes them
as given and reads them only when it copies a Class into a Child's Content slice (ADR-0013). This file is the whole
contract; the file and folder names live in `ContentLayout`, and `ContentLayoutTests` pins them.

## Layout of a Board container

| Path | What | Read by the API |
|---|---|---|
| `catalog.json` | A JSON array with one object per Book | Yes |
| `refined/{book_id}/{chapter_id}.json` | One refined Chapter: its Concepts and Q&A | Copied unchanged |
| `summaries/{book_id}/{chapter_id}.json` | The Chapter summary; one for every refined file | Copied unchanged |
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
- Both Boards: a refined file with no summary of the same name is a broken master, and a Class with no built Book cannot
  be copied.

## `manifest/class-{N}.json`

Schema id `class-manifest-v1`, camelCase, in the shape recorded in ADR-0014. The example
[`class-manifest.cbse-7.json`](class-manifest.cbse-7.json) is parsed by the API's own tests, and the content build keeps
a copy of it: change the format in both places. The file's `class` must equal N, or the copy fails.

## What a copy does with it

Everything is read and checked before the first write. The files above are copied byte for byte, the slice's
`catalog.json` keeps only the entries of the Class, and the slice records the version it was copied from. The master is read twice, and
a master that changed in between fails the copy, so upload content while no Child is being added.
