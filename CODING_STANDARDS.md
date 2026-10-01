# Coding standards

Read during review. These are judgement calls; anything a linter or CI check can enforce belongs there instead.

## C#

- Write less code. Use a small, safe, well-known package before hand-writing boilerplate. Map records to storage entities and DTOs with Mapster (`Adapt<T>()`), not manual `ToX` methods.
- A failure the caller must see is an exception, not a `bool` or a status code. Example: `AlreadyRegisteredException` maps to 409.
- A fixed set of values is an enum, not a static class of string constants.
- Keep classes short and single-purpose. Keep concerns separate.
- Names used as cipher context (row keys such as `profile`) stay fixed in code. They are part of the encryption's authenticated data, so never make them configurable.
- Comments: only a rare one-line why (a security property, a platform quirk, a decision). No banner comments, no XML summaries that restate the code.
- Do not add checks that the platform already enforces (for example, what JwtBearer validates).

## Security and endpoints

Every new endpoint follows these. `/security-review` checks them before the PR (step 6 of `docs/agents/workflow.md`).

- **No cross-family reads.** Every read and write is scoped to the caller's own Family, taken from the caller's identity and never from a request field. A caller who sends another Family's id gets nothing back (404, not 403). Never build a query, path or filter by concatenating request text; use typed parameters and the repository's fixed keys.
- **Stateless and idempotent.** No per-instance in-memory state, because the app can run more than one replica. A repeated call is safe, as starting a Class twice is.
- **Bounded.** Set a maximum body size, maximum lengths and counts on every input, and page every list response.
- **Rate limited per caller**, keyed by the caller's uid hash, never by raw uid or IP alone.
- **Cancellable.** Every I/O call takes the `CancellationToken`. No unbounded waits.
- **Crypto ships with its tests.** Code that encrypts, hashes or wraps keys has tests for tamper detection, wrong key, wrong authenticated data, and isolation between Families. Never invent a primitive; use the platform's and Key Vault's.

## Changes that need docs in the same PR

A change to the API, infra or data layout updates the root `README.md` (Status and runtime sections), the decision log, and any README or skill that describes it.

## Direct Anthropic API calls

One attempt per call with no retries, a hard budget ceiling checked before each call, the cheapest adequate model, minimal targeted prompts, and a guard so an empty reply never overwrites existing content. See `docs/agents/workflow.md`.
