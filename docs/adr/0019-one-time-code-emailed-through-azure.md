# The API emails a One-time code through Azure Communication Services, and a sensitive operation requires its proof

Narrows [ADR-0015](0015-recovery-code-is-the-only-family-key.md) and D113: Lantern still never sends or sees the Recovery code or any key, but it may now send an email that carries no key.

Some operations cannot be undone or hand someone else access: a Child's Hard delete and a Family delete ([ADR-0003](0003-deleting-a-child.md)), and later adding a second Parent. A tap on a button, or a stolen sign-in token, must not be enough. The Parent has to show that they can open their own inbox. The mechanism knows nothing of any one operation (D126), so each new operation reuses it.

On request, `POST /v1/security/code` takes an **operation** (a lowercase kebab name the caller chooses, such as `delete-child`) and an optional **target** (an id the operation acts on). The API makes a 6-digit **One-time code** from a cryptographic random source and emails it to the address in the validated sign-in token, which must be marked verified. The email names no operation, so a caller cannot put text of their choosing into a Parent's inbox. The Parent's stored email is a locked value Lantern cannot read, so it is not used. Sending sits behind `IEmailService`, implemented over Azure Communication Services Email. The API returns a **challenge** to the phone: the operation, the target, an expiry of 5 minutes, a random nonce, and an HMAC-SHA256 keyed by `Security:Key` over those, the Parent's uid hash and the code. The phone holds the challenge in memory only, and Lantern stores nothing: no table row, no id (D125).

`POST /v1/security/code/verify` takes the challenge and the typed code, recomputes the HMAC, and on a match returns a **proof**: a signed blob bound to the Parent's uid hash, the same operation and the same target, valid for 5 minutes. An endpoint that needs a proof asks `IOneTimeCodeService` for one for its own operation and target, and returns 403 without it. `DELETE /v1/family/children/{childId}` requires operation `delete-child` with that Child's id; `DELETE /v1/family` will require `delete-family` (#188). Start again deletes through `DELETE /v1/family`, so it goes through the same code. The code is never logged, stored, or returned; the challenge cannot be turned back into it without the key.

Tries are limited by rate, not counted. Per Parent, a new code can be requested once a minute and five times an hour, and the check allows five attempts a minute, each answered 429. Across a 5-minute challenge that is about 25 guesses of 1,000,000 (0.0025%). The limiters are in memory per replica, as the existing ones are, so a restart or a second replica loosens them; accepted for now.

The operation is free-form and has no rules of its own at request time. The API does not check that the target exists or belongs to the caller before it emails a code; the endpoint that acts on the proof does, as every endpoint already scopes to the caller's Family. A caller can therefore ask for a code for an operation no endpoint uses, which is harmless: a proof only opens an endpoint that asks for that exact operation and target.

Why Azure Communication Services Email: Lantern already runs on Azure, so there is no new vendor, no new account and no new processor; the price is about $0.00025 an email plus $0.00012 a MB, roughly ₹21 for 1,000 codes; and it can start on Azure's own managed sender domain, so no domain has to be bought yet. The founder chose this on 2026-10-31 while still thinking about buying `lanternapp.in`.

Considered and rejected: Brevo (a free plan of 300 emails a day and a plain REST call, but a new processor, and a sender that is trusted only once a domain is authenticated, which is the purchase the founder is deferring; it remains the swap behind `IEmailService` if Azure proves unfit); the Firebase Trigger Email extension (Firestore, Cloud Functions on Blaze and an SMTP provider as well, for the same result); a Firebase Auth email link (a link, not a typed code, and account linking with the Google sign-in); a code made and checked on the phone (friction only, since the delete endpoints would still accept a stolen token; the founder asked again on 2026-11-01 and chose the stateless challenge instead); a stored code row with a try counter and a single-use proof (the first design; replaced by D125 to avoid storing and maintaining ids); an enum of operations with a rule class for each, or a switch in the service (D126 chose the free-form operation so that adding one needs no change to the shared service).

What it costs, accepted: Azure's managed domain is meant for testing, with limits on how many emails can be sent and how often, and its mail may land in spam, so a real Family may not see its code. It is acceptable for UAT. Before real Families, add a custom domain to the same resource (DNS records for SPF and DKIM) and change the sender setting; no code changes. An email outage stops these operations, and nothing else; the screen says the email could not be sent. The privacy notice names Azure Communication Services as handling the Parent's address.

Who holds what:

| Attacker holds | Gets | Why |
|---|---|---|
| A stolen sign-in token only | Cannot do a sensitive operation | The endpoint needs a proof, and a proof needs the code from the inbox |
| The Parent's Google account and inbox | Can | Both factors sit in one account. Accepted cost, the same as ADR-0015 |
| The Parent's unlocked phone, signed in, with the mail app | Can | The phone's own lock is the defence |
| Another Parent's sign-in | Nothing | Challenges and proofs carry the signed-in Parent's uid hash |
| A proof for one operation or target | Cannot do another operation, Child or the Family | The proof is bound to its operation and target |
| A stolen token and a challenge, guessing | About 25 tries of 1,000,000 per code | The check is rate limited per Parent; the HMAC cannot be tested offline without the key |
| Whoever can read Key Vault | Can send email as Lantern | The Communication Services connection string is a Key Vault secret the template writes; the apps read it with their identity. Azure has no built-in role that lets an identity send email without Contributor, so a key is used |
| `Security:Key` | Can sign proofs | The same key that guards the uid hash; leaking it is already a rotation event |
| A proof, replayed within 5 minutes | Can repeat the same operation on the same target | Accepted: deleting a Child twice is safe, and a future operation must be safe to repeat or add its own guard |
| A stolen token asking for codes | Fills the Parent's inbox, 5 an hour | Accepted: it cannot read them |
