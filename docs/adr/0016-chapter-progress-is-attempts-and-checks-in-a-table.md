# A Chapter's progress is its Attempts and Checks, kept in a `progress` table, and its state is derived

A Parent can plan a Chapter again after a Check, and retry the Check once the Child is prepared. Every go at it has to be kept with its marks, for diagnostics later, and the Chapters list has to say where each Chapter stands without opening anything else. This ADR records where that lives. It replaces D22's idea of "Class-prefixed rows in the Child's History partition" for Chapter progress, and #88's "progress is kept in the Child's History". It changes nothing about the Workspace blobs (ADR-0002, ADR-0013).

## Decision

- **A dedicated `progress` table**, partitioned by the Family id (the same partition key as `families`). A Family delete is one partition sweep and a Child delete is a sweep of its row keys (ADR-0003).
- **Three row kinds**, each under the Child and Class, so one range read gives a screen what it needs:
  - `s_{child}_{class}_{chapter}`: the Chapter's summary row. It holds facts about the latest Attempt (Concepts done and count, Checks in it) and totals (Attempts, Checks, the latest score, the last activity). The Chapters list reads these.
  - `a_{child}_{class}_{chapter}_{attempt:000}`: one row per Attempt, which carries the Plan's id and the Concepts marked done with their times.
  - `c_{child}_{class}_{chapter}_{attempt:000}_{checkId}`: one row per Check, with every question's Concept id, title and mark, the score and the version of the Content slice it was taken on. Check rows are never changed.
- **The state is derived, never stored**: no summary row is Not started; the latest Attempt has a Check is Checked; every Concept of it is done is Taught; otherwise Planned. A new Plan on a Checked Chapter starts a new Attempt, so the Chapter reads Planned again while the earlier marks stay in its history.
- **One table transaction per change**, in the Family partition: start an Attempt (Attempt row and summary), mark a Concept done (Attempt row and summary, each with its ETag) and record a Check (Check row and summary). A lost race reads again and retries. Repeats change nothing: an Attempt is started once per Plan id, a Concept is done once, and a Check is keyed by an id the caller supplies.
- **No personal data in a row**: ids, Concept titles, marks and times only.
- **The reads** are the Chapters of a Subject (state, Attempt count and Concepts done per Chapter, and the Subject's counts) and a Chapter's history, both paged and both behind the Family and Child checks of the Subjects API.

## Considered and rejected

- **History blobs** (the Workspace, ADR-0002): the delete sweep is free, but every update rewrites a whole file, and a list of a Child's Chapters or a trend across Chapters would need many reads or a second index.
- **Azure SQL, for now.** A Child's whole history is a few thousand small rows read by key, which Table Storage serves in one range read, so SQL would add a resource, migrations and a standing cost to every environment (D10, ADR-0001 chose Table Storage for the same reason). It would earn its place for queries across Children or Families, aggregations over large histories, constraints across partitions, or reporting tools. Revisit it when one of those is a real requirement, and then feed it from the progress writes (a "Check recorded" message through ADR-0004's queue) instead of moving the system of record.
- **Interleaving Attempt and Check rows under one prefix**: one read for a Chapter's history, but two row types in one query. Two small reads keep every query typed.

## What it costs, accepted

- A Child's rows are removed by a handler as well as a blob sweep, so the delete handlers each do one more thing.
- Reading a Subject's Chapters reads the Child's summary rows every page. They are small and few.
- Plan content, when the agents write it, will live in the Class folder as blobs referenced by the Attempt's Plan id. Its layout belongs to the Plan work.
