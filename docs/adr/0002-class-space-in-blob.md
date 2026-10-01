# A Class space is a path in the `family` Blob container

A Child's Class space is the Blob path `family/{familyId}/{childId}/{class}/`. Blob has no empty folders, so the space exists once its marker blob `class.json` exists. Starting a Class writes the marker with `If-None-Match: *`: the first start creates it, a repeated Class finds it and changes nothing, so the space (and what later lands in it) is reused.

One operation, `IClassSpaceStore.StartAsync`, starts a Class. Registration calls it for each Child, and a later Class change will call the same method. Registration starts the spaces before it writes the Parent's profile row, which stays the commit point from ADR-0001: a failed start leaves the caller unregistered, and an attempt that fails later leaves only unreachable markers under a Family id nobody holds.

The `family` container is private and created by Bicep. The API's managed identity gets Storage Blob Data Contributor from `bootstrap.sh`; no key or connection string exists in Azure.

Considered and rejected: a Table row per Class space (the files need a Blob path anyway, and a second record would have to be kept in step with it), and creating the space on first file write (a Child would have no space until something is written, so "every Child has a space" would not hold).
