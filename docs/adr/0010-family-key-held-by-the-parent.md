# The Family key is held by the Parent, not by Lantern

Lantern stores a Family's personal details but cannot read them. The phone makes a random Family key, locks the personal fields with it, and wraps the key twice before registering: once with a key derived from the Passphrase and once with a key derived from the Recovery code. The API stores the locked values and the two wrapped copies (with their salts) exactly as sent, and `GET /v1/me` returns them to the Parent who owns them. Key Vault no longer holds or unwraps a Family key; it keeps only the secret that hashes a uid.

What is locked: the Parent's name and email, and each Child's name, birth year and School. What stays readable, because the server needs it to enforce limits and choose Books: Class, Board and Region. The fields keep their plain names (`Name`, `Email`, `BirthYear`, `School`); only the value is locked, so the code reads the same before and after. School is always sent (the phone locks an empty one), so the server column is never null. The server bounds each value's length and nothing else: it cannot check a birth year it cannot read, so that check lives in the app.

The phone picks the Family's id before it registers, because the first Child is locked in the same call and the Passphrase is combined with that id. The API accepts a well-formed id that does not exist and refuses one that does (`409 family-id-taken`); the Family row is an Add, so a race has one winner. The Board is chosen by the Parent at registration and has no default; a stored Family row with no Board reads as CBSE. An SSC Family can hold Children in Classes 1 to 5 only (`400 class-not-available`).

Deleting a Family still erases the rows and the Workspace; crypto-shredding now means clearing the wrapped keys, after which nothing the sweep leaves behind can be opened.

There is no migration: no pilot Family exists on the Key Vault scheme, and UAT test Families are deleted. The old `family-field-key` stays in the vault, unused, until someone removes it.

Considered and rejected: keeping a Key Vault key per Family (Lantern can read everything, which the founder did not want), a key in the phone's keystore only with no Passphrase (a lost phone loses the Family), and email recovery of the Passphrase (Lantern would have to know it). Decision D66 in the decision log carries the reasoning; the Passphrase screens come with their own stories.
