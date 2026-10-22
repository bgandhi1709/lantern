# The Recovery code is the only way into a Family

Supersedes the two-wrapped-copies part of [ADR-0010](0010-family-key-held-by-the-parent.md): the Family key is wrapped once, by a key derived from the Recovery code, and there is no Passphrase.

The phone makes the Family key and the Recovery code, locks the personal fields with the key, wraps the key with a key derived from the code (Argon2id over `{family_guid}_{recovery_code}` with its own random salt, the cost fixed by the `v1.` prefix as before), and registers. The API stores the locked values and the one wrapped copy with its salt exactly as sent, and `GET /v1/me` returns them. Opening the Family on another phone derives the key from the typed code and unwraps the Family key on the phone, so Lantern never sees the code or the key.

The Parent must email the code to their own address before registration continues. The phone's own mail app sends it from the Parent's own account (a `mailto:` link, with the share sheet as the fallback), so the code never passes through Lantern. The phone cannot see that the message was sent, so Continue waits until the Parent has opened the email and ticked that they sent it. ADR-0010 rejected email recovery because Lantern would have had to know the secret; here Lantern sends nothing.

Why: a Parent who forgets a Passphrase is locked out of their Family, and a code in their own inbox cannot be forgotten. The founder chose this on 2026-10-22 after the Unlock screen was built with both.

What it costs, accepted: whoever can open the Parent's email can open the Family, because the code in the inbox is the key. Before, a stolen inbox alone was not enough.

The cost, walked through as abuse cases (the walk the Security step now requires):

| Attacker holds | Gets | Why |
|---|---|---|
| The code only | Nothing | `GET /v1/me` gives the wrapped key only to the signed-in Parent, so there is nothing to open |
| Another Google account and the Parent's code | Nothing | `/v1/me` returns that account's own Family; the code opens only the Family it was made for (tested in `unlockSecurity.test.ts`) |
| The Parent's Google account only | Class, Board and Region, which Lantern can read; no names | The wrapped key comes back but the code is needed to open it |
| The Parent's Google account and inbox | Everything the Parent sees | Accepted cost: both factors sit in one account |
| The Parent's unlocked phone | Everything the Parent sees | The Family key is in the Keystore; the phone's own lock is the defence |

The API cannot tell a second phone signed in as the same Google account from the Parent's own: unlock happens on the phone and sends nothing, so Lantern holds no device list and cannot revoke one. The Parent is never told. This is recorded, not solved: options (a devices list, new-device approval, a delayed release, rotating the key) are in the experimental issue #168. A Family whose Parent loses the code and the phone starts again as a new Family (Settings, Start again). The code is 16 characters from a 32-character alphabet (80 bits) behind a slow Argon2id derivation, so a stolen wrapped key stays slow to guess.

API contract: `PassphraseWrappedKey` and `PassphraseSalt` are removed from the register request, the Family model and the stored row; `RecoveryWrappedKey` and `RecoverySalt` stay. There is no migration, because no pilot Family exists; UAT test Families are deleted.

Considered and rejected: keeping the Passphrase as an optional second key (the screen that causes the lockout stays, and the founder wants one path), and Lantern sending the email from the server (Lantern would see the code).
