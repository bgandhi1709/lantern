# Lantern stores each kind of data where it fits: Azure SQL, Cosmos DB and Blob

Lantern held everything in Table Storage and Blob. A Child's whole Class was copied into the Family's own storage
(ADR-0013), which made the 500-Family load test cost about Rs 680 and left a new Child waiting for a background copy.
Family rows were held together by ETags, a commit-point row and hand-written sweeps.

Lantern now stores each kind of data in the layer that fits it (spec #204, D136):

- **Azure SQL** holds what is relational: Family, Parent, Child, teaching progress and the action ledger. An action and
  the change that caused it commit together.
- **Cosmos DB** holds a Board's content as documents, one shared copy that every Child of a Class reads. The `content`
  container is partitioned on `/classKey` (for example `cbse-3`) and holds five kinds of document: board, class, book,
  chapter and plan ([`contracts/README.md`](../architecture/contracts/README.md)). The class document is written last,
  and a Class is ready only once it exists. Only a loader writes it.
- **Blob** holds media: videos, posters and Figures, and the Board containers the content build uploads to.

Bicep creates every store and the app never does, so a missing store is a loud failure. Cosmos DB uses the free tier, with
no account key: callers sign in with Entra ID and a data role. Client types of every store stay inside the repository
layer (ADR-0005).

Content reaches Cosmos DB once, from the `ncert` and `ssc` containers, by a Python script (D136), and a Board added later
is loaded straight into Cosmos DB. The content build is not changed (ADR-0017).

This ADR records the decision. Each story of the epic replaces or amends the ADRs it affects when it ships: ADR-0001, 0013,
0014 and 0016 are replaced and ADR-0002, 0003, 0004, 0008 and 0017 are amended, the SQL stories among them.
