# Working cycle

One issue, one pass through these steps. Work inline: no subagents unless the user asks for one by name.

| # | Step | How | Output |
|---|---|---|---|
| 0 | Ticket | File or pick the issue first. Small scope. | Issue with label |
| 1 | Grill | `/grill-with-docs`, at most 15 questions. Stop early once decisions settle. Terms go to `GLOSSARY.md`, hard-to-reverse calls to an ADR. | Decisions |
| 2 | Tech pass | Agent lists the areas the issue touches and loads only the matching skills from the routing table. | Skills loaded |
| 3 | Plan | Plan in chat: behaviours to test, files touched, risks. User approves in one word. | Approved plan |
| 4 | Implement | `/implement`, driving `/tdd` one red-green slice at a time. Build and run single tests as you go, the full suite once at the end. | Commits on the branch |
| 5 | Verify | `/verify` against local Docker. Write down every test scenario. | Scenario list |
| 6 | Security | `/security-review` on the branch diff, before the PR. Fix findings in the same branch. Check the security rules in `CODING_STANDARDS.md`. | Findings fixed or recorded |
| 7 | PR | `/pr` with the scenarios and acceptance criteria. | PR |
| 8 | Doc sync | Root `README.md` (Status and runtime), `docs/ideation/decision-log.md`, ADRs, any README or skill the change touches. | Docs match the code |
| 9 | Review | User reviews on GitHub. Fix, reply as Claude, resolve the threads. | Resolved threads |
| 10 | Retro | `/retro` in the same session, before `/clear`. Mechanical misses become checks, judgement calls go to `CODING_STANDARDS.md`. | Environment fixes |

Skip `/to-spec` and `/to-tickets` at this scope. Use them only when the work spans several sessions.

## Divergence guard

The established path is whatever is recorded in `GLOSSARY.md`, `docs/adr/`, `docs/ideation/decision-log.md`, the approved plan, or `CODING_STANDARDS.md`.

When an idea, request or edit would depart from it, in ideation or in implementation:

1. Stop. Do not write the code.
2. Name the established decision and where it is recorded.
3. Grill: ask why it should change, what the established path fails to do, and what the change costs. At most 3 questions.
4. Ask the user to rethink: keep the established path, or supersede it deliberately.
5. Proceed only after an answer. If the decision is superseded, record it first: append a dated decision-log entry, or add an ADR for a hard-to-reverse call. Never rewrite history.

Applies to architecture, data layout, domain terms, the cycle itself and the coding standards. It does not apply to details the record does not cover.

## Tech routing (step 2)

| Issue touches | Skills |
|---|---|
| Any code | `ponytail`: reuse and simplify before adding code |
| Any C# | `uncle-bob-clean-code`, `modern-csharp-coding-standards` |
| HTTP endpoints, contracts | `minimal-api` |
| External calls, retries | `resilience`; the API-call guardrails below override its retry advice |
| Storage, keys, access | `azure-table-storage`, `azure-key-vault`, `azure-rbac`, `lean-azure-infra` |
| Service boundaries, async flows | `microservices-architect` |
| Ask API, retrieval | `rag-architect` |
| Agent runtime | `multi-agent-patterns`, `context-optimization`, `evaluation` |
| Python | `python-pro` |
| React | `vercel-react-best-practices`, `vercel-composition-patterns` |
| Module shape, testability | `codebase-design` |

Load a skill only when the issue touches its area. Each loaded skill costs context for the rest of the session.

## Token management

- **Context window:** keep steps 1 to 3 in one unbroken window, then `/clear` between issues. Each issue starts from the ticket, `GLOSSARY.md`, ADRs and this file, not from chat history.
- **Where memory lives:**
  - Durable decisions go in the repo: `GLOSSARY.md`, `docs/adr/`, `docs/ideation/decision-log.md`. They load only when relevant.
  - Working preferences live in the native memory index (`MEMORY.md`), which is small and loaded every session.
  - claude-mem is disabled: its startup injection cost about 23k tokens per session. Past-session history stays in `~/.claude-mem` and in the transcripts under `~/.claude/projects/`; re-enable the plugin only to search it.
- **Reads:** read the specific lines you need, not whole files. Search before reading.
- **Output:** short chat replies, normal prose in files and commits.
- **Tests:** single test files while iterating, the full suite once at the end.
- **Direct Anthropic API calls** (scripts, runtime features):
  - one attempt per call, no retries;
  - a hard budget ceiling checked from the API's real `usage` field before each call;
  - the cheapest adequate model by default (Haiku 4.5 for mechanical work);
  - send only the field or page that is wrong plus the rule it broke;
  - cache stable system prompts;
  - never let an empty reply overwrite existing content.
