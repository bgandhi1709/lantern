# Deleting a Child is a hard, asynchronous delete

A Child's data belongs to the Parent. Deleting a Child removes its Child row, every Class space blob under `family/{familyId}/{childId}/` and, once it exists, the Child's History partition. Nothing is kept as a tombstone, a soft-deleted copy or a "just in case" backup. The Answer library (anonymous Scrubbed Questions and tagged Answers, D23) is never touched: nothing in it links to a Child, so a delete code path keyed by `familyId/childId` cannot reach it, now or when later features add data to it.

A delete can be long, and it must not fail halfway. So the API only records the Parent's intent: it writes a pending-delete row (partition `deletes`, row `{familyId}_{childId}`, carrying the `familyId` taken from the caller's token), marks the Child row `Status=deleting` and returns `202`. A `BackgroundService` in the API container claims a pending row with an ETag and a lease, sweeps the Blob prefix, sweeps the History partition, deletes the Child row and deletes the pending row last. Every step is idempotent, so a crash at any point resumes cleanly, and a failed pass retries after its lease expires. A `deleting` Child is hidden from `/me`, cannot be edited, cannot be re-added under the same id, and a Class start must refuse it.

The Parent's intent is the commit point, not the cleanup: once the pending row exists the delete will finish even if the request that wrote it failed afterwards.

Every Child operation takes the Family from the caller's token, never from the request. A Child id that is not in the caller's Family returns 404, the same as an unknown id, and writes nothing.

Anything that later stores data under a Child adds its cleanup to the worker's step list. Blob soft delete and versioning stay off on the `family` container, since either would keep deleted content.

Considered and rejected: Quartz (a second async mechanism whose in-memory store breaks the stateless rule), Queue Storage now (no new Azure resource is needed yet; the worker can move onto a queue when something else needs one), a synchronous delete (a long sweep would fail the request and leave a partial delete), and Parent-pair approval (any Parent maintains the Children, as in the glossary).
