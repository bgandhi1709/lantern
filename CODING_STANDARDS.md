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

## Changes that need docs in the same PR

A change to the API, infra or data layout updates the root `README.md` (Status and runtime sections), the decision log, and any README or skill that describes it.

## Direct Anthropic API calls

One attempt per call with no retries, a hard budget ceiling checked before each call, the cheapest adequate model, minimal targeted prompts, and a guard so an empty reply never overwrites existing content. See `docs/agents/workflow.md`.
