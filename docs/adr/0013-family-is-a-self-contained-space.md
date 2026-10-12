# A Family is a self-contained space: each Child's Class folder holds its own Content slice

Everything a Family needs to run lives inside that Family's own storage. Book content is no exception: when a Child is added, and again at each Class change, Lantern copies the Books for that Child's Board and Class out of the shared `ncert` or `ssc` container into the Child's Class folder. Plan, Ask and Check read the copy and never the shared container. The shared containers stay the build output (`ncert-build upload`) and the source of the copy, nothing more.

The reasons are not performance. Reads of read-only files cannot race, and a catalog kept in memory (D82) costs no Blob reads once warm. The reasons are:

- **Blast radius.** A bad yearly upload no longer reaches every Family at once.
- **A frozen year.** A Child's Chapters do not change under them mid-year.
- **A complete delete.** Hard delete and Family delete already sweep a Blob prefix (ADR-0003), so the copy goes with them.
- **Independence.** A Family stands on its own, and nothing it reads is shared with another Family.

## Layout

```
family/{familyId}/{childId}/{class}/
  class.json          the folder marker (ADR-0002), unchanged
  content/            the Content slice, owned by the copy, never by a Child's own files
    board.json
    catalog.json      trimmed to the Books of this Board and Class
    toc/<book>.json
    refined/<book>/<chapter>.json
    summaries/<book>/<chapter>.json
    guidance/<book>.json
    version.json      the shared container's version the copy came from, written LAST
```

Only JSON the runtime reads is copied. `text`, `chapters`, `drafts`, `bundles` and `pictures` are build intermediates; Lantern has no picture-based questions yet, so figures stay out. Adding `pictures/` later is one change to the copy list. The copy is byte-for-byte, so D50's cached prompt prefix still matches across Families.

`content/version.json` is the readiness marker. Its absence means the slice is not ready. `class.json` keeps meaning only "the folder exists".

## What is copied, per Board

The two masters differ, checked against UAT's `ncert` and `ssc`, so each Board has its own rule list in the code.

| | NCERT (CBSE) | SSC |
|---|---|---|
| Books | catalog entries of the Class that have refined Chapters (Class 3: 3 of 97 entries; NCERT lists every Book in every medium and builds only some) | catalog entries of the Class (the catalog holds only built Books; one with no refined content fails the copy) |
| Per Book, required | `refined/{book}/*`, and a `summaries` file for every refined Chapter | the same, plus `toc/{book}.json` and `guidance/{book}.json` |
| Once, required | trimmed `catalog.json` | `board.json` and the trimmed `catalog.json` |
| Never expected | `board.json`, `toc`, `guidance` (NCERT has none; a stray one is not copied) | none |

For both: a missing required file, or a Class with no built Book, fails the copy and writes no `version.json`; the trimmed catalog keeps each entry as the master has it; the slice equals what the master holds for those Books, not the catalog's declared Chapter range (NCERT `jewe2` declares 9 Chapters and 7 are built). Class 3 on UAT: NCERT 78 blobs (about 403 KB), SSC 97 (about 646 KB).

## How the slice is made

One action, one handler. `create-workspace` (ADR-0004) gains steps: create the folder marker, copy the slice, write `content/version.json` last. Registering and adding a Child both already record this action, one per Child, so a registration with several Children starts all their copies at once, each Child with its own slice (two siblings in one Class hold two copies).

A Child's Class is fixed for now: the API already refuses to edit it, and moving a Child to a new Class is not built. If it is built, it re-enters through the same action with the new Class, so the old Class folder and its slice stay and History keeps pointing at what it was written against; that is a later decision, not made here.

The copy reads each file whole and writes it as one Put Blob. A Class is at most about 1.2 MB in at most 150 files (measured on UAT's `ncert` and `ssc`), so a server-side copy would save nothing and add authorisation to get right; whole files also keep two copies of one file at once from colliding, which a streamed upload in blocks does not (found by a test). A retry recopies every file, with no per-file tracking.

### Concurrency

- **No lock on the shared containers.** They are only read, and a lease would make every Family wait on one writer. Nothing is locked per Child either: each copy is idempotent, so two of the same message at once end with the same bytes.
- **The master must not change during a copy.** The plan (every blob read, with its ETag) is made again after the copy and must match the first, or the slice is not marked ready and the queue retries. This is checked by the system; the maintenance-window rule for the upload stays as the way to avoid the retries.
- **A cap on load.** Across all copies in a process, at most 32 Blob requests are in flight (16 per Child), on top of the host's 4 concurrent messages. Throttling replies are retried by the Azure SDK with backoff and then by the queue.
- **Tables.** The handler only adds two point reads (the Child's status and the Family's Board) and the existing ledger delete, so it adds no table contention. Registration stays one transaction in the Family's partition, and add-Child stays guarded by the Family row's ETag (ADR-0001, ADR-0003). The handler checks the Child is still active, and sweeps what it wrote if the Child was deleted meanwhile (ADR-0002). Retries are the queue's: a failed message waits out the five-minute lock and comes back, up to 288 deliveries, then the ledger row lets the next API start send it again (ADR-0004). No new mechanism.

## Readiness

A Child is registered before its slice is ready (the Workspace is created in the background). Until `content/version.json` exists, a Plan call is refused with `content-not-ready` and the app shows a loading state, not an error. The blob is the only record of readiness; no flag is kept on the Child row.

## Updates

Not built with the first stories: the copy at registration and add-Child ships first, and the refresh below (with the master `version.json` it compares against) is a later story. Until then a slice's `version.json` records when and for which Board and Class it was made, and nothing refreshes it.

A slice is **pinned**: a yearly upload changes nothing under an existing Family. The master container gets its own `version.json` written last by `ncert-build upload`, and each slice records the master version it was copied from. A founder-run `ncert-build refresh --board <board>` sends one `refresh-content` message; the handler lists that Board's slices, compares versions, and queues one refresh per stale slice, each with its own ledger row. A Class folder with no `content/` counts as stale, so the same command backfills Children registered before this decision. A refresh overwrites only `content/`, never a Child's own files, and writes `version.json` last.

The upload runs only in a maintenance window, with the refresh after it. A Family that registers during the upload may take a mixed slice, but it is stamped with the older master version, so the refresh finds it and redoes it.

## Supersedes

D82 for a Child's reads: the API no longer serves a Child from the shared catalog. The shared containers are read only by the copy and the refresh. D50 stays true in effect (one prompt prefix per Board) but is no longer served from one shared catalog.

Considered and rejected: a whole-Board copy per Family (bytes no Child reads), one slice per Family shared by its Children (a refcount, and a Class change of one Child would touch another), reading the copy with a fallback to the shared container (it brings the shared dependency back and hides copy failures), refreshing every slice automatically on upload (it recreates the blast radius), a Table row or a `ContentReady` column for each slice (a second record to keep in step with the blob, as in ADR-0001 and ADR-0002), and a copy made inside the request (a long copy would fail the request, ADR-0004).
