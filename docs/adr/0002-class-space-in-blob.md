# A Workspace is a path in the `family` Blob container

(First written for the "Class space"; D37 renamed it Workspace and widened it to the Child's whole area.)

A Child's Workspace is the Blob path `family/{familyId}/{childId}/`, with one folder per Class, `{class}/`. Blob has no empty folders, so a Class folder exists once its marker blob `class.json` exists. Creating it writes the marker with `If-None-Match: *`: the first call creates it, a repeated Class finds it and changes nothing, so the folder (and what later lands in it) is reused. The container keeps the name `family` (set in the bicepparam as `containers.workspaces`): renaming it would leave every existing Workspace behind.

One operation, `IWorkspaceStore.CreateAsync`, creates a Class folder, and only `Lantern.Functions` calls it (D33). Registering or adding a Child records a `CreateWorkspace` action in the ledger before the Child rows, then sends it after them (ADR-0004); the handler creates the folder only while the Child row exists and is active, so a failed add leaves no marker, and it sweeps again if a delete finished meanwhile. A later Class change will record the same action. A Child therefore gets its Workspace shortly after it is added, not before: whatever later writes into a Workspace must cope with it not existing yet. (Until D33 the API created them inside the request, before the commit point.)

The `family` container is private and created by Bicep. The managed identity gets Storage Blob Data Contributor from `bootstrap.sh`; no key or connection string exists in Azure.

Considered and rejected: a Table row per Class folder (the files need a Blob path anyway, and a second record would have to be kept in step with it), and creating the folder on first file write (a Child would have no Workspace until something is written).
