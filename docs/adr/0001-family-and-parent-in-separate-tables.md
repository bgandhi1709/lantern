# A Family and its Children live in their own partition, apart from each Parent

Children used to sit in the Parent's partition (keyed by the hash of the Parent's uid), so a second Parent would have held a second copy of every Child. A Family now has its own partition in a `families` table (the `family` row with Region, the wrapped Family key and its `KeyScheme`, one `parent_<uidhash>` membership row per Parent, and one `child_<id>` row per Child), and the `parents` table keeps only each Parent's own settings and their `FamilyId`.

Table transactions cannot span partitions, so registration writes the `families` batch first and the Parent's profile row last. That row is the commit point: until it exists the caller is not registered, and an attempt that loses a race deletes the rows it wrote.

Considered and rejected: SQL for Family, Parent and Child (one transaction, real foreign keys) and a separate link table. SQL adds a resource and reverses the Table Storage choice in D10 for a write that happens once per Family; a link table is what the membership rows already are, without a third table.
