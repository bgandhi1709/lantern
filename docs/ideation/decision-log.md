# Ideation decision log

This log records decisions from the design sessions. Each entry has a matching GitHub issue labelled
`decision`. Revise an entry by adding a new, dated one below it rather than rewriting history.

## 2026-09-23: first ideation session

### D1. Who Lantern is for
The users are parents like **Mother (35)**, who studied in Gujarati medium and reads basic English.
Her child is **Kid (6)**, in CBSE Class 1, which includes French. Parents can read and use a
smartphone. What they find hard is **explaining concepts**.

The tool is for the **parent**, not a tutor for the child. Lantern is non-commercial and funded by
helper groups and the founder.

### D2. Product split
Lantern is built as six pieces, each with its own spec:
1. Ask & Explain
2. Homework help
3. Daily plan & focus
4. Syllabus prep
5. Progress
6. NGO/admin

The pilot starts with the foundation, then Ask & Explain.

### D3. Onboarding journey
1. Sign in with Google (Firebase).
2. **Choose the language first**, so the rest of onboarding appears in that language.
3. Give consent (DPDP).
4. Answer questions about the child: age, school and class. More than one child per parent is
   allowed.

Answers are stored as **raw, append-only** data, which is the base of the intelligence stack.

### D4. What can be worked out from onboarding
- The school gives the board and CBSE affiliation.
- The class gives the NCF-FS / NCERT framework and learning outcomes.
- The age gives attention span and how to teach.
- The date gives the likely point in the school year.
- The language gives the words to use for explanations.

These can't be worked out: the actual books (private publishers are common, and French is chosen
by the school), the current chapter, and the child's level.

### D5. Book inventory
For each book the mother photographs the **cover and the contents page**. AI agents identify the
book and search the internet for its chapter map and teaching style. A book found once is reused
for everyone at the same school and class.

### D6. Grounding is the core principle
Books exist to give the AI context, so it **does not hallucinate** for a child. Answers match the
book's method, words, scope and level.

### D7. The context funnel
**Navigation is context:** `/kid` → subject → chapter → question. Each level narrows what the AI
can say. Subject instructions are fixed rules. General curiosity questions get their own area.

### D8. Tag and reuse
The same question asked in different words is recognised **inside a funnel node**, tagged by
concept and intent, stored, and reused. Stored answers are generic to a book, chapter and language,
and are shared across families. Personal context is added only when an answer is shown. Parents'
👍/👎 feedback keeps quality up.

### D9. Architecture: data is ore
1. Each input becomes an event and is stored raw first.
2. The event goes on a queue.
3. A handler (orchestrator) refines it into memory files for the child and the mother.
4. Personal data is kept in an encrypted identity vault, with an encryption key per family, so
   deleting the key erases the data (crypto-shredding).
5. AI agents work only on refined data.

### D10. Stack
- Azure: Container Apps with one container (not App Service or AKS), and **one storage account**
  holding Blob, Table Storage (not Cosmos DB) and Queue. (D31: the queue for actions is Azure Service Bus
  Basic, and a second Container App runs `Lantern.Functions` beside the API.)
- Key Vault holds a single master key, which encrypts each family's key.
- Firebase for authentication.
- A modular **ASP.NET on .NET 10** app in **one repository**, following the conventions of
  `vantage-freight-hub`.
- Separate GitHub Actions workflows for deployment. Infrastructure in Bicep with `.bicepparam`
  files.

### D11. AI route
- **Microsoft Foundry** for development and the pilot, because there is no direct Anthropic key
  yet.
- A task-based AI gateway, with the model chosen per task in configuration.
- Claude Opus 5 by default, with effort tuned per task. The funnel becomes the prompt-cache prefix.
- Rough cost: about ₹2–3 for each new answer, about ₹0 for a reused one, and roughly ₹35–160 per
  active family per month.

### D12. Pilot
Six mothers, mostly friends. Every question, answer and rating is logged, and that log becomes the
test set for trying cheaper models later.

### D13. Name
**Lantern**: a simple English word that carries the idea of a *diya*, a lamp lit at home.

### D14. First batch flow
1. The API writes the raw event and puts it on the queue, then returns `202`.
2. A worker (`BackgroundService`) handles the event with guarded handlers. (D31: `Lantern.Functions`
   handles it, dispatched by message type, and nothing runs on a timer.)
3. **SignalR** notifies the app.

A notification is only a hint: the app then fetches the state it points to. A status endpoint is
the fallback. For the pilot SignalR runs inside the app, behind an `INotifier` interface. Push
notifications through FCM for when the app is closed come later.

## 2026-09-24: second ideation session

### D15. NCERT backbone and cross-grade routing
NCERT textbooks for CBSE Classes 1–10 are loaded once and become the shared base layer for every
family. Private-publisher books are mapped onto NCERT concepts later. The goal is a baseline, not
full coverage: questions the base layer can't answer are logged and handled by hand.

The backbone is a **concept graph**: concept nodes, where each concept is taught (class, book,
chapter), and prerequisite links between concepts. Every question is routed in this order:

0. **Answer cache.** A stored answer for a close-enough earlier question, with the same stage and
   language, is returned with no model call.
1. **Local check.** Concepts in the child's current chapter.
2. **Global graph.** Concept embeddings across all ten classes. Matches below threshold τ mean the
   question is **deferred** and logged (a reject option). Among matches within δ of the best score,
   the one **nearest the child's class** wins, their own class first.
3. **Grade scaling.** The anchor is only the source of facts. The answer is always written for the
   **child's class**, taken from the profile. An anchor above that class is simplified along the
   shortest prerequisite path from what the child already knows. An anchor below it gets an older
   tone, but no facts beyond the anchor.
4. **Tag and store.** The question is tagged with its concept, anchor class, child class, stage
   and language. The answer is stored by concept + stage (NCF-2023 stages: 1–2, 3–5, 6–8, 9–10) +
   language, so a Class 10 child is never served a Class 3–5 answer.

Matching is exact nearest-neighbour search in memory, which is fast at a few thousand concepts, so
no vector database is needed. τ, τ_cache and δ start as defaults and are tuned from the pilot's
rated log (D12). Opus does the analysis once, at build time. At question time, Opus runs at low
effort, and only on a cache miss.

### D16. Strategy: build the pristine layer first
The next build is the NCERT layer and plain **text Q&A**, ahead of the rest of the foundation:
- NCERT content comes from the PDFs' own text layer. There is **no OCR, page-image analysis or
  book-cover photo step** for now.
- The mother types a question and gets an answer. She can ask as many questions as she likes.
- Every answer gets **👍 / 👎**. The ratings measure quality and tune the routing thresholds.

Voice, photos, book discovery and the other pieces from D2 come later, on top of this layer.

## 2026-09-28: super context (issue #41)

### D17. Earlier-concepts scope: same book only, never cross book boundaries
The super-context earlier-concepts index only looks at prior chapters of the *same* book. A
chapter never pulls context from a different book, even one in the same class (e.g. an EVS
chapter never informs a Math chapter). This keeps the prerequisite chain matching how a single
subject actually teaches, and avoids false-concept bleed between unrelated subjects. Cross-subject
linking is a concept-graph problem (D15), not a bundle-context problem.

### D18. A/B test book: bejm1 (NCERT Class 2, Joyful Mathematics, English)
Class 1 (`aejm1`) is excluded as the super-context A/B subject: the ncert-refine skill always
leaves `prerequisites` empty for Class 1, so it can't exercise the earlier-concepts index at all.
`bejm1` is the first Class 2+ book alphabetically, same subject line as the already-piloted
`aejm1`, and already fully bundled (11 chapters) — so the A/B test only needs a few prior chapters
refined before the target chapter, not a fresh pipeline run.

### D19. Known gap: quote-vs-pick validation stays inside the closed-8B rule
Python checks that a picked earlier-concept quote appears verbatim (whitespace-normalised) in that
concept's source chapter text. It cannot check that the *pick itself* — which earlier concepts are
relevant to the current chapter — is correct; that judgment stays with the 8B model, bounded only
by "quote or don't get credit," not by a second independent check. This is accepted as a known
limitation rather than solved now: revisit only if the A/B test (D18, issue #41 step 5) shows it
producing bad context, not preemptively.

### D20. Skill invocation: super-context bundle path as an explicit argument
`ncert-refine` takes the `bundles-v2/` path as an explicit argument when super context is in play,
rather than SKILL.md being changed to auto-discover a sibling bundle-v2 file by convention. This
keeps the skill's existing contract (and its Class 1 behavior, D-prerequisite handling) unchanged
for every caller that doesn't pass the new argument.

## 2026-09-30: registration and workspace design

### D21. Family is a group, Parents are equal members
A **Family** groups Parents and Children, like a resource group. Each **Parent** has their own settings (Language, Consent); Region belongs to the Family. "Mother" and "Kid" are personas in these docs, not roles in the model, and there is no Admin. The POC allows one Parent per Family, and the layout stays open for more. See `GLOSSARY.md` and ADR-0001.

### D22. Class spaces and History
A Child's **History** (Scrubbed Questions, Tests, Class changes) is kept across Class changes as long as the Child is in Lantern, and is deleted with the Child. Each Class has its own **Class space**: Blob `family/{familyId}/{childId}/{class}/` and, later, Class-prefixed rows in the Child's History partition. Only the Parent changes a Child's Class; a repeated Class reuses its space. Built in issue #49 and the Ask API (#35).

### D23. Scrubbing and shared intelligence
Only the Child's and Parent's personal fields are encrypted. A Question is scrubbed by rules (the Family's known names, School and Region, plus phone and email patterns) before anything is stored; the raw Question is never stored. Per-Child History stays in the Family's space, and an anonymous store of scrubbed, tagged Questions and Answers is shared across Families with no link back. A client-held Family key is tracked in issue #47; the Family key cache in #52.

## 2026-10-01: Class space (issue #49)

### D24. A Class space is created at the start of a Class, before the registration commit
Registration starts one Class space per Child through the same operation a later Class change will use, and does it before the `parents` profile row so a failure leaves the caller unregistered. The marker is an empty `class.json`; the container name `family` is a constant, not a setting. See ADR-0002. Supersedes nothing; it builds the Blob side of D22.

### D25. Security review before the PR, and security rules for every endpoint
The working cycle gains step 6, `/security-review` on the branch diff before the PR (the later steps renumber). `CODING_STANDARDS.md` gains a "Security and endpoints" section: no cross-family reads, stateless and idempotent, bounded input and output, rate limit per caller, cancellation, and crypto code ships with tamper, wrong-key and isolation tests. Crypto tests are a standing rule, not a one-off audit. Records the user's request of 2026-10-01; changes the cycle recorded in `docs/agents/workflow.md`.

### D26. Local sign-in and key stand-ins, local E2E before UAT
Local Docker gets the Firebase Auth Emulator and a PEM-file `IFamilyKeyWrapper` instead of a shared-secret bypass, and a one-shot `seed` command writes a dev Family (two Parents, ten Children in Classes 1 to 10, a Class space each). The goal is a full end-to-end run locally (`deploy/local/e2e.sh`) before anything goes to UAT. The emulator issues unsigned tokens, so the API accepts them only when `Firebase:EmulatorHost` is set, and refuses to start with that or `KeyVault:LocalKeyPath` outside Development; the `seed` command refuses to run outside Development. Considered and rejected: a UAT-valid shared secret (a second way in on a public environment holding real encrypted Family data; the keyless OIDC path already reaches UAT) and a Key Vault emulator container (HTTPS and certificate trust for no extra coverage, since UAT E2E runs the real client). Registration now also checks "already registered" before it writes Class spaces, so a repeat call leaves no markers behind.

### D27. Dev and test support lives outside `Lantern.Api` (supersedes where D26 put it)
Review of PR #61: production code must not carry test or dev-only code. The Key Vault stand-in, the Auth Emulator token handling, the seeder and the second-Parent write moved out of `Lantern.Api` into the host project, and `apps/lantern-api/Lantern.Api.Test.Integration` holds the E2E tests (UAT and local, one suite). `Lantern.Api` keeps only two more `InternalsVisibleTo` lines. The stand-ins sit in `Lantern.Api.Test.Integration.Host`, a small local host that runs the API through `WebApplicationFactory` on Kestrel (an ASP.NET hosting startup was tried first: it registers before the API, so the API's own Key Vault wrapper won). The local Docker image (`deploy/local/api.Dockerfile`) runs that host; the production image does not contain it. `IFamilyRepository.JoinAsync`, the dev options, the startup guard and the `seed` command are gone. D26's goal (full E2E locally before UAT) and its rejected alternatives stand. Rule recorded in `CODING_STANDARDS.md`.

### D28. E2E runs in Docker and gates the release; no E2E against real UAT (issue #62)
Supersedes the keyless E2E against UAT after every deploy (from #46). The same `Category=E2E` tests run against the Docker stack (Azurite, Firebase Auth Emulator, key-file Key Vault stand-in, seeded dev Family) on every PR and push, and `image` waits for them. After the gated deploy, a smoke check (`/health/live` 200, `/v1/me` without a token 401) and a read-only role preflight run inside the `deploy` job. The cloud-only plumbing is removed: GitHub OIDC token minting and UAT row cleanup in the fixture, the `uat-e2e` environment and credential, the cleanup Storage Table role, and the Firebase OIDC provider setup. Reasoning (the user's): UAT roles and permissions are static and hardly ever the reason for a failure, and the UAT testing done by hand later validates the real environment. Accepted cost: the Docker stack cannot see real-Azure problems such as a missing role (the failure that prompted this, PR #61); the preflight and smoke check cover the cheap cases.

### D29. One release workflow: infra and API deploy together (supersedes the separate `infra.yml`)
The infra workflow and the API workflow ran independently, on different concurrency groups, and collided on `ca-lantern-uat` when one merge touched both (`ContainerAppOperationInProgress`, run 36877313150). `infra.yml` is deleted. `api.yml` now also triggers on `infra/**`, an `infra-check` job lints and runs `what-if` before the release, and the gated `deploy` job applies `infra/main.bicep` with the new image and port in a single deployment instead of `az containerapp update`. The role preflight and the smoke check stay. Decided by the user on 2026-10-01 because two workflows were confusing and the cloud E2E that justified separating them is gone (D28). Cost: an infra-only change now also builds and tests the API and ships a new image.

### D30. Managing Children: hard, asynchronous delete; the Answer library is kept (issue #51)
A Parent adds, edits (Name, School, BirthYear; never Class) and deletes Children. Add takes a client `childId`, so a repeat returns the existing Child, and an ETag-checked update of the Family row in the same batch holds the limit of 6 under concurrent adds. Delete is a hard delete done asynchronously: the API writes an action-ledger row, marks the Child `deleting`, sends a `child-delete` message and returns `202`, and `Lantern.Functions` finishes the Blob, History and row cleanup, retrying until done (D31 replaced the first version, a `BackgroundService` on a timer). The Answer library (the D23 anonymous store) is never deleted with a Child. Every Child call scopes to the caller's Family from the token; a foreign or unknown id is 404. A Family may end with no Children. See ADR-0003.

### D31. Event-driven actions: Service Bus Basic queue and `Lantern.Functions` (replaces the timer worker of D30; amends D10 and D14)
The user rejected the `PeriodicTimer` worker in the #51 PR: the architecture should be event driven, not timer driven. The user chose Azure Service Bus on the Basic tier, accepting that it is not free (Basic is queues only; about ₹0.05 a month for 3,000 messages at ₹88 to the dollar), then refined it into a typed-message dispatcher: one queue `lantern-actions`, messages with a `type`, an Azure Functions app `Lantern.Functions` that dispatches by type, and a transactional ledger table in the existing storage account that keeps each action idempotent. The Functions app runs as its own Container App in the existing environment, scaling to zero on a KEDA Service Bus rule (idle cost about ₹0; ₹380 a month if kept warm). Shared code moved into `Lantern.Core`. The user decided to do this inside the #51 PR rather than as a follow-up ticket, because it is an architectural pivot of the delete and not a new feature. D10's "Queue" on the storage account and D14's `BackgroundService` worker are superseded for actions by this: the queue is Service Bus, and the worker is `Lantern.Functions`. Topics (pub-sub) wait for a second consumer of the same event and Standard tier. See ADR-0004.

### D32. The API is layered with generic services (amends D10's "modular ASP.NET" and the folder layout of #51)
The user found the API layer bloated and asked for a layered architecture that is easy to extend and test without fragmenting, keeping every earlier decision. Four projects: `Lantern.Api` (controllers and the API models, the only shapes the app sees), `Lantern.Core` (service models, contracts, the generic `ServiceBase<T>`, crypto, a generic `ServiceBusService`, the action dispatcher), `Lantern.Repository` (entities, `UnitOfWork<TEntity>`, the generic `BaseRepository<TModel, TEntity>`, field encryption) and `Lantern.Base` (`FamilyService`, `ChildService`, validators, action handlers). Encryption moved from the services into the repository with the stored bytes unchanged. Key Vault sits behind `IKeyVaultClient`, the one thing local runs and tests replace. Registration lives in `FamilyService`; an interactor waits until a service grows too complex. Done inside the #51 PR, as an architectural pivot of the feature in flight. See ADR-0005.

## 2026-10-02: Class spaces start in the Functions app; handlers live there; tests leave apps/

### D33. A Class space is started by `Lantern.Functions`, not inside the request (supersedes D24's ordering)
The user moved the Blob write out of the request: adding or registering a Child now records a `ClassSpaceStart` action (`class-space-start` on the queue) in the ledger before the Child rows, writes the rows, then sends it; the Functions app writes the marker. The ledger row is the commit point, as for a delete, so a crash after the rows still gets the Child its space, and a message for a Child whose add failed does nothing. The handler reads only the Child's status (no Family key, so the Functions app still needs no Key Vault access), and sweeps the space again if a delete finished while it was writing. "Every Child has a Class space" becomes "every Child gets one shortly after it is added"; anything that later writes into a space must cope with it not existing yet. Registration no longer fails when Blob storage is down. See ADR-0002.

### D34. Action handlers and the dispatcher live in `Lantern.Functions` (amends ADR-0005)
Finishing an action is the Functions app's job, so `IActionHandler`, `ActionHandler<T>`, the dispatcher and the handlers (`ChildDeleteHandler`, `ClassSpaceStartHandler`) moved from `Lantern.Core`/`Lantern.Base` into `Lantern.Functions/Handler`, registered by `AddLanternFunctions()`. `Lantern.Core` keeps the publish side the API needs (`ActionMessage`, `ActionType`, payloads, `IActionPublisher`); services and repositories stay in `Lantern.Base` and `Lantern.Repository` so both apps reuse them. The queue name is no longer a constant: Bicep passes the queue it creates as `Actions__Queue` to both apps, the publisher reads `ActionOptions`, and the trigger reads `%Actions:Queue%`.

### D35. Test projects live under `tests/`, not `apps/`
The user wants `apps/` and `libs/` to hold production code only. The four test projects moved to `tests/lantern-api/` (mirroring `apps/lantern-api/`), including `Lantern.Api.Test.Integration.Host`, the local Docker host. The CI guardrail now greps all of `apps` and `libs` for test-only code.

### D36. Every environment value is written once, in the bicepparam (supersedes D24's "the container name is a constant")
The user asked that no environment value be hard-coded. Table names, the Workspace container, the Key Vault secret and key names, the queue (name, lock, deliveries, TTL), each app's CPU, memory and replica range, the children rate limit and the action resend delay now live in `infra/params/lantern.<env>.bicepparam`. Bicep creates each resource from it and passes the name or value to the apps as an env var; `bootstrap.sh` reads the Key Vault names from the same file. The options classes have no defaults and validate on start, so a missing setting stops the app instead of falling back silently; `appsettings.json` holds only empty keys, and local values live in `appsettings.Development.json` and `deploy/local`. GitHub Actions carries only per-run values (images, port) and its own sign-in, because a bicepparam change shows in the PR and in `what-if`. Not configurable, on purpose: storage formats (row keys, cipher context, marker and column names), the wire contract (action names, problem codes, routes) and product rules (six Children, ages 3 to 18); changing any of those is a code change with tests or a migration.

### D37. "Class space" becomes **Workspace**; actions are Workspace events on one `workspace-events` queue
The user renamed the Child's area: a **Workspace** is a Child's whole area (`family/{familyId}/{childId}/`), with one folder per Class, replacing "Class space" (which named one Class's folder). The two actions are `CreateWorkspace` (`create-workspace`, at registration and add) and `RemoveWorkspace` (`remove-workspace`, the Child delete), handled by `CreateWorkspaceHandler` and `RemoveWorkspaceHandler` with `CreateWorkspacePayload` and `RemoveWorkspacePayload`; the store is `IWorkspaceStore` (`CreateAsync`, `RemoveAsync`). There is still exactly one queue, now `workspace-events`, and the dispatcher picks the handler by the event type. The ledger partitions are the new type names, which is safe only because #51 is not yet in UAT. The Blob container stays `family` so existing Workspaces in UAT are kept. The HTTP API still speaks of Children (`/v1/family/children`).

## 2026-10-02: Releases are checked on the PR and a PR can be released to UAT (issue #68)

### D38. A PR labelled `deploy-uat` releases to UAT; Azure prerequisites are checked on every PR (amends D29)
The release of #66 failed after merge three ways that the PR never exercised: a missing identity role, an unregistered `Microsoft.ServiceBus` provider, and (when retried from a branch) an Azure sign-in that the app registration does not trust. `infra/preflight.sh` now checks providers and roles in `infra-check` on every PR, and `bootstrap.sh` registers the providers. The user asked for PRs to be deployable to UAT before merge: a PR labelled `deploy-uat` runs the same release (images pushed, one Bicep deployment, smoke check) behind the same `uat` approval. It needs no new federated credential, because the PR's jobs sign in as `pull_request` and the deploy as `environment:uat`. UAT stays one shared environment, so the last approved release wins. A manual run on a branch other than `main` cannot sign in, so it is not a supported way to deploy.

## 2026-10-02: One trace across the queue (issue #70)

### D39. OpenTelemetry to Application Insights, one trace from the request through Service Bus into Functions ([ADR-0006](../adr/0006-one-trace-in-application-insights.md))
The user wants one request to be one trace. Both apps export OpenTelemetry to a workspace-based `appi-lantern-<env>` on the existing Log Analytics workspace, signed in with the managed identity (no ingestion key), off when no connection string is set. The recording request's `traceparent` is stored on the action ledger row so a resend, which starts its own trace, links back to it. Retention and a daily cap are bicepparam values (D36). Re-run `bootstrap.sh` before the release: the identity needs Monitoring Metrics Publisher.

## 2026-10-03: Deleting a Family (issue #53)

### D40. A Family delete unregisters its Parents in the request and erases the rest in Functions; no Family key cache (supersedes #52 in D23)
`DELETE /v1/family` records a `RemoveFamily` action in the ledger (the commit point, as in ADR-0003), deletes the profile of every Parent of the Family and returns `204`. With no profile, every later call finds the caller not registered, so the Family needs no status column and no request pays an extra read to check one. The user chose this over a `deleting` status for that reason. `RemoveFamilyHandler` removes any profile left behind, clears the wrapped Family key first (crypto-shredding, so anything a failed sweep leaves is unreadable), deletes every row of the partition, then sweeps `family/{familyId}/`. A profile that already points at a new Family (the Parent registered again before the handler ran) is kept. The controller that serves the Family, `/v1/register` and `/v1/me` is now `FamilyController` (singular: a caller has one Family). The Family key cache (#52) was dropped: one Key Vault unwrap per request is cheap at pilot scale, #47 moves the key to the device after the pilot, and a cache would keep a deleted Family's key alive on other instances.

## 2026-10-03: Plan, Ask, Check (ideation, spec #79)

### D41. Lantern's value is the Plan → Ask → Check loop
The founder reframed Lantern around how a Parent actually teaches: plan a Session for one Chapter, answer the Child's unplanned questions, then check whether the teaching landed. Each Check feeds the next Plan, so the Parent can focus on weak areas. The flow guides the Parent rather than asking her to describe anything (D1's Mother): pick a Child, a Subject, a Chapter, read the Chapter summary, tap Plan (D7, navigation is context). New terms are in the glossary: Plan, Chapter summary, Recall, Check, Weak Concept, General question. Test keeps its meaning (preparing for a school test).

### D42. One Plan per Chapter; Recall after each Concept; a scored Check per Chapter
A Plan covers one Chapter, with its Concepts in book order and progress kept per Concept, so it can span several sittings. One or two unscored Recall questions follow each Concept to keep the Child engaged. The Check comes at the end of the Chapter, with questions from across its Concepts; the Parent asks them aloud and marks each right or wrong, so the Child never signs in and nothing is graded by AI. Each question keeps its Concept, giving a score per Chapter and per Concept. A Chapter can be checked again; every Check is kept, and the Chapter list sorts weakest first with plain sorting.

### D43. Ask is bounded by the open Chapter; General questions wait for real data
The open Chapter is the context, like a Claude Code session with its project loaded. Asks are kept against the Chapter like notes. A question outside the Chapter is a General question: logged and not answered in the pilot, until real-world data shows what Parents ask.

### D44. English for the pilot; a language Subject in its own language
Plans, Recall, Checks and Answers are in simple English, served from the refined corpus. A language Subject (Hindi, French, Sanskrit…) is always answered in that Subject's language. The Parent's chosen Language waits for a later iteration.

### D45. Pilot on 2026-12-03, Android and web from one Expo codebase, in UAT
The D12 pilot with a few Families launches on 2026-12-03 (milestone #7). The parent app is React Native with Expo: Android through Play internal testing and the web build; no iOS store build, but the code stays iOS-compatible (ADR-0007). The pilot runs in the UAT environment; a separate prod environment is revisited before the pilot widens.

### D46. Plan, Ask and Check agents write with a model, grounded in the Chapter (supersedes D15's Opus at question time for Chapter Ask)
A fixed Plan would go stale, so a Plan agent writes each Plan from the Chapter's data, the Child's Class, Weak Concepts, past Asks and an optional scrubbed note from the Parent (at most 300 characters, treated as context, never as instructions). A Plan is made on first open, on New plan and after a Check; otherwise the stored one is reused. The Check agent builds the Check paper from the Plan as taught, the Asks and the Concepts' Q&A; scoring stays the Parent's taps. All agents use Claude Haiku 4.5 with the Chapter as a cached prompt, and cite Concept and Q&A ids so every item traces back to the book. Chapter summaries are written once, offline, with the Batch API.

### D47. A Node.js agent service with a targeted, run-scoped API; direct Anthropic API (amends D10, supersedes D11's Foundry)
The agents run in their own Container App, `lantern-agents`, in Node.js with TypeScript, so the founder can review the agent layer (no meaningful performance gain from Python or .NET for I/O-bound orchestration, and it shares the Expo app's language). It has a read-only identity and internal ingress; `Lantern.Api` does every write. Callers authenticate with their managed identity and pass only a run id; the run (Family, Child, Chapter, kind, state, expiry) fixes the scope, and the model never sees or chooses an id. MCP was judged too broad; a hosted MCP server, a forwarded Parent token and a shared privileged token were rejected. It calls the Anthropic API directly through the SDK's Tool Runner; the Claude Agent SDK (built-in file and shell tools) and Managed Agents (beta, per-session containers) were rejected for now. A sidecar in the API's app was rejected because managed identity is per app. See ADR-0008.

### D48. Plan generation is a job, with run states and bounded retries (amends the API-call guardrails)
`POST …/plan` writes a pending run, queues it and returns `202`; a `Lantern.Functions` worker with a concurrency cap calls the agent service, stores the Plan and marks the run; the app polls. This gives backpressure under evening load and survives a dropped mobile request. Run states are pending, running, succeeded, grounding_failed and failed; a stuck run is marked failed by the next `GET` (no timer, per D31). A repeated `POST` returns the existing run or Plan. Runtime agent calls retry only on 429 and 529, at most twice with backoff, with the budget checked before each; scripts keep zero retries. Every Plan stores `promptVersion`, `contentVersion` and `model`.

### D49. A Session allowance per Family
Each Family has a monthly Plan count and a daily Ask cap, set in the bicepparam with no defaults; a failed Plan doesn't count. One allowance for every pilot Family; Tiers and sponsor reports wait for pilot data. D1 (non-commercial, sponsor-funded) is unchanged.

### D50. AI cost comes first in every design
At 3,000 Families (about 60,000 Sessions a month) AI is roughly 95% of running cost, about ₹2.6 lakh of ₹2.65 lakh a month (₹88 to the dollar, list prices); compute, logs and storage are the rest. The founder made lowering it a standing rule: every story and decision that touches a model call states its cost per call and extracts the most from each one: reuse before calling (stored results, pre-written Q&A, the Answer cache), a prompt cache shared across Families (stable prefix, per-Family data after the breakpoint), short structured outputs, the Batch API for offline work, and tokens logged per call. The rule lives in `docs/agents/workflow.md` under token management.

## 2026-10-04: SSC Board (ideation, spec #93)

### D51. Lantern is content-neutral; Book content comes only from its rights holder ([ADR-0009](../adr/0009-content-only-from-rights-holders.md))
After the promoters asked for ICSE, the only bulk source found (studiestoday) showed commercial publishers' books (Selina, Frank, Morning Star…) with no licence. Lantern is a technology that ingests and explains Books it does not own; each source is taken from its rights holder (NCERT, ebalbharati) or from a school that states it holds the rights. Mirrors are never a source. ICSE waits for a school bringing its own material.

### D52. The school-funded model is a direction for after the pilot
Investors and supporters suggested private schools (Podar and smaller ones) pay, and Lantern stays free for State Board families on that money. The pilot on 2026-12-03 stays as planned (D45); a School space for a school's students comes after the first demo. D1's non-commercial, sponsor-funded stance is unchanged until that is designed.

### D53. SSC Board: English medium, Classes 1–5, core Books from ebalbharati, in the pilot
The second Board is the Maharashtra State Board (SSC), English medium, Std 1–5, 2026 editions: English Balbharati and Maths for Std 1–5, EVS Parts 1 and 2 for Std 3–5 (16 Books). Std 3 EVS Part 1 is printed per district; the Mumbai edition is used. Language, art, PE and work-education Books are out. Older Children are more independent, so SSC targets young Children first. SSC is in the pilot with at least one SSC Family, in its own milestone (#8) due with the pilot.

### D54. Board is a Family setting, fixed for the pilot (ADR-0009)
One Family follows one Board, picked at registration; existing Families are CBSE. It cannot change in the pilot; a wrong pick is a Family delete and registering again. An SSC Family's Children are in Class 1–5.

### D55. Local models for everything before refine; Haiku 4.5 Batch for refine
Download, split, extract, segment, draft, pictures, bundle and Book guidance run on local Ollama (`qwen3:8b`, `qwen3-vl:8b-instruct`), so Claude spend goes only to refine and Chapter summaries. Refine uses Claude Haiku 4.5 through the Batch API after a 5-Chapter quality gate, capped at ₹1,250 ($3 left plus a $10 top-up, at ₹96 to the dollar). `qwen2.5:7b-instruct` was removed from the WSL Ollama as unused.

### D56. Each Board keeps its own structure; agents route prompts by Board
The founder does not want SSC forced into NCERT's shape. A Board profile per Board records how its Books are built, and Plan, Ask and Check prompts are selected by Board. The shared minimum is Chapter → Concept → Q&A with ids, so Plan, Recall, Check and grounding by Concept id work everywhere. The cached prefix is per Board and still shared across every Family on that Board (D50). For English Balbharati the Chapter is the Unit (about 20 pages, lessons as sections), not each one-page lesson.

### D57. Book guidance is extracted and given to the agents
Balbharati Books carry the publisher's notes for teachers and parents, in the front matter and in boxes beside lessons. They are cut out by the local model, verified word for word against their page, and stored as one file per Book linked to the Book and Chapter, for the agents to read as context, never as instructions. The same for NCERT is a later follow-up.

## 2026-10-05: Android app POC (spec #103, issue #104)

### D58. The POC app is Android only; the web build waits (amends D45 and ADR-0007)
The Android app POC (milestone #9) is demoed from the emulator and then a real phone, so the Expo web build is deferred. It is still one Expo codebase in `client/lantern-android` that uses only libraries supporting Android, iOS and web, so the web and iOS targets stay a build target away. The pilot's Android Play internal testing track is unchanged; D45's web build is no longer a pilot commitment until it is brought back.

### D59. One button: Continue with Google
The Start screen has a single "Continue with Google". There is no Register and no separate Log in: the first sign-in that finds no Family (`404 not-registered` from `GET /v1/me`) starts registration, and the account is created by `POST /v1/register` at the end of it.

### D60. A Parent stays signed in until Sign out
Firebase keeps the sign-in on the device and renews the short-lived ID token itself; the app asks for the current token before every API call. The session ends only on Sign out or when Firebase revokes the account. When Lantern cannot be reached the Parent stays signed in and sees "Can't reach Lantern" with Try again; only Firebase saying "not signed in" returns to Start. The unreachable behaviour is a proposed default, waiting for the founder to confirm.

### D61. The scaffold ships as a signed APK from GitHub Actions; the UAT address is a secret (issue #104)
Even an empty app must be deployable, so the scaffold story includes the pipeline. `android.yml` checks every PR that touches `client/**` and, on a manual run behind the `uat` environment's approval, builds a release APK signed with one stable key (so the SHA-1 Firebase sign-in needs never changes) and uploads it as an artifact that expires after one day. The repo is public and any signed-in GitHub user can download its artifacts, so the UAT address is a secret on the `uat` environment (masked in logs, set by `ci/setup-signing.sh` from the deployed Container App), not a variable and not in code. It is baked into the APK, so it is readable by whoever holds the APK; it is not a credential, because every call still needs a Firebase sign-in. A private Blob container was rejected for now: it needs new Azure resources and a role for something that guards no credential. The placeholder screen calls `GET /health/live` to prove a phone reaches UAT, with a 45 s timeout for UAT's scale-to-zero cold start (about 25 s measured), and a release build refuses a non-HTTPS address. Container Apps cannot host an Android app, which is an APK on the phone; only a future web build could be hosted, as static files. The `minReplicas: 1` option for demos stays a bicepparam choice for later.

## 2026-10-05: App look (prototype)

### D62. The app takes the new five-screen look; Plan, Ask and Check stay as recorded (D41, D42, D43)
The founder preferred a new design for the Android app (Inter, blue primary, soft cards, bottom tabs) and asked for the onboarding prototype to be redrawn in it, with Home, Plan, Ask, Check and Progress added. The design's "Chapter Preparation" five cards and "Oral Test" were not adopted: the Plan stays per Concept with Recall (D42), and the assessment stays the Parent-asked Check, marked right or wrong, never graded by AI, with no Child sign-in. The bottom tabs are Home, Teach (Plan and Ask), Check, Progress and More; the design's Test tab is renamed because Test means preparing for a school test. The design's Reports tab and star difficulty are left out. Progress lists Chapters weakest first with the latest Check score (D42).

### D63. A Parent switches Child from any tab; a new Parent lands on a first-time Home
The Child's name sits at the top of Home, Plan, Ask, Check and Progress; tapping it opens a sheet to switch Child or add one (a Family holds up to six Children). Plan, Ask, Check and Progress follow the active Child, and the choice carries across tabs. Right after registering, Home has nothing to continue, so it shows the Child, a Subject choice and a short Plan, Ask, Check explanation; Continue Plan, Weak Concepts and Recent activity appear after the first Plan. Choosing a Chapter after the Subject (D41) is not drawn yet.

### D64. The First registration screens are split across #105 and #106; more Children can be added at registration
The prototype's First registration journey (Opening, Start, About you, Board, First child, first-time Home) is built by #105 (Opening, Start, Home top bar) and #106 (About you, Board, First child, first-time Home); #104, the scaffold, stays closed. The First child step carries an optional accordion for up to six Children in all. The API's register call takes one Child, so the first Child goes in `POST /v1/register` and each extra goes to the add-Child endpoint afterwards; a failed extra does not undo the registration. The Board cards sit side by side and the Maharashtra card shows the board's logo, pending confirmation that Lantern may use it (ADR-0009). No screenshots were attached; the tickets link the private canvas.

### D65. The Expo app gets its own standards skill and the official Expo skills
The founder is new to Android and React Native and cannot review the app as closely as the C#. So the app is built to written rules: `.claude/skills/expo-app-standards` covers structure, clean TypeScript, the API boundary and local storage, and the PR carries a "Why this way" note for each new library or pattern. App work has its own cycle, `docs/agents/app-workflow.md`. The official `expo` and `typescript-lsp` plugins are installed for the matching task skills and code intelligence; the third-party React Native skill bundles shown in the plugin list were not installed until they are read. Local storage is the strictest rule: tokens only in the Firebase SDK's store or `expo-secure-store`, no personal data on the device (a Child is held as its opaque id), one storage module, everything wiped on sign out, Android backup off. Where a rule can be a lint rule it becomes one in the first app story.

## 2026-10-05: Family passphrase

### D66. The Family locks its personal data with a Passphrase on the phone; Key Vault no longer holds Family keys (issue #47)
Supersedes D9 and D10 for the Family key (a Key Vault master key wrapping each Family's key, unwrapped on the server on every request) and replaces #47's plan (Android Keystore key plus recovery code, migrate after the pilot). The founder's reasoning: keep the encryption Lantern already has, but let the Parent hold the key, so Lantern needs no key and cannot read the data. A Parent chooses a Passphrase once, like a Git key, and the phone does the locking and unlocking; the server stores what it cannot read. Decisions:

- **Per Family, not per Parent.** The Family's Passphrase is combined with the Family's GUID as `{family_guid}_{passphrase}` so two Families with the same Passphrase get different keys, and a Family holds at most two Parents who share it. Joining is a later story.
- **No change, no reset.** The Passphrase can never be changed; it is the Parent's responsibility and the UI says so. A forgotten Passphrase with no Recovery code means a new Family.
- **Recovery code.** Shown once after the Passphrase is set; the Parent saves it with the phone's share sheet. Lantern never emails the Passphrase or the code: emailing would mean Lantern knows it. The Passphrase is at least 8 characters, typed twice.
- **What is locked.** Parent name and email, Child name, birth year and School. Class, Board and Region stay readable by the server, so it can still run the Ask API and enforce limits on them. Scrubbing or masking Questions is a later part.
- **Wrapped Family key.** The phone makes a random Family key and stores it on the Family row twice, wrapped once by a key from the Passphrase and once by one from the Recovery code. Lantern holds only those wrapped copies, so it cannot open them, and a new phone gets its key by signing in and typing the Passphrase or the code. The phone makes the Family's GUID before registering, because the first Child is locked in the same call.
- **On the phone.** The Family key is kept in `expo-secure-store` so the Passphrase is asked for only on first setup, a new phone or after sign-out (a wrong Passphrase is tried on the phone, never sent). A new stored value, approved by the founder.
- **Production.** No migration: there is no pilot Family on Key Vault to move, and the scheme is the same in production. UAT test Families are deleted.
- **Screen.** The Passphrase is a new step 3 of 4 between Board and First child, so #106's three steps become four and #106 waits for it. The canvas has the new board (5 Passphrase).
- **Later invite.** The second Parent joins by in-person approval from Parent 1, not by email. Bluetooth and a QR code are both candidates; Bluetooth needs a native module and a dev build, a QR code needs only the camera. Compared when that story is picked up.

An ADR is written when the work is picked up: hard to reverse, with real alternatives.

### D67. The web build is dropped; the app is Android and iOS only
Supersedes the web part of ADR-0007 (the Expo web build for PC and any phone without the Android app, and "every library must support web"). The founder is sticking to Android and iOS. Pilot is Android; iOS stays a later build target, and the rule "nothing Android-only" stays. ADR-0007 carries an amendment pointing here, and the dependency rule in `app-workflow.md` and `expo-app-standards` now reads Android and iOS.

### D68. The app collects anonymous usage and performance numbers with Firebase Analytics, only after the Parent agrees
The founder wants to learn from the first release how easy the app is to use: how long a Parent spends on each screen and section, how many taps a step takes, where they stop, plus the phone model and performance (app start, slow calls). Nothing personal. The rules:

- **Events carry names, never people.** Screen viewed (with time on screen), button tapped (the button's name), registration step reached or finished, and Firebase Performance Monitoring's app-start and network timings. No name, email, Child detail, Class, Board, Region, text a Parent typed, or user ID is ever sent, as an event value or a user property.
- **No advertising.** The advertising ID and ad personalisation are off in the app. Analytics is not used for ads.
- **Consent first.** Collection is off in the app from install and starts only after the Parent ticks the privacy notice on About you; the notice lists what is collected. The opening screens collect nothing.
- **What Google adds on its own.** Analytics records the phone model and Android version, and the approximate place the connection comes from (city level, from the network address, not GPS). The phone model needs the property's "granular location and device data collection" setting on, which also allows the city; the founder accepts that. The privacy notice says so.
- **Not in scope.** Crash reporting (Crashlytics) is a separate choice and is not enabled. This is separate from the server's telemetry (D39, ADR-0006).
- **Effect on #113.** Analytics and Performance Monitoring are native Firebase modules, so the Firebase SDK choice in #113 is expected to be `@react-native-firebase` with a development build. The decision is still made there.

Considered and rejected: collecting from the first launch (the opening screens would send data before any Consent), and Analytics with the advertising ID left on (nothing needs it).

## 2026-10-06: Refining the SSC Books (issue #97)

### D69. SSC refine runs on Claude Sonnet 5.5 through the Batch API at medium effort (amends D55)
D55 chose Haiku 4.5. A comparison on five Chapters (Claude Code subagents, no API spend) showed Haiku writing no home activity (`try`) on any of 261 Q&A and splitting Concepts the way the local draft did, while Sonnet wrote one for nearly every Q&A and taught through the book's own examples. A Batch API gate then ran the same five Chapters at low, medium and high effort ($0.42): all three were grounded in the book; high gave the richest answers but broke the limits most and wrote the fewest activities; medium wrote the most activities (79% of Q&A) at $0.027 per Chapter. The founder chose medium. The full run of 202 Chapters cost $4.84 (₹464 at ₹96); the gate's medium output was reused for its five. The request carries the rules as the system prompt, the Chapter without the local draft (13% of input, and it anchored the model's grouping), a JSON schema, and ids filled in by code. Chapters failing `check` get tiny per-field calls (`fix_flagged.py --board ssc`), or a rerun at low effort when the Concept or Q&A count is wrong. The Concept limit now scales with the Chapter: 1–8 for a one- or two-page Chapter (a single idea such as Std 1 "My Village"), 2–12 for one over 12 pages, 2–15 for an English Unit (11 to 13 lessons, each Concept naming its lesson), 2–8 otherwise; the same Chapters came back over the old limit at both medium and low effort, so the limit, not the model, was wrong. All 207 SSC Chapters pass `check`; the whole refine, gates and fixes included, cost about $5.65 (₹542).

### D70. `check` flags only markdown-shaped text in answers
The rule rejected any `*`, `_`, `#` or backtick, so fill-in blanks ("My name is ___") and symbols a book uses ("place 1, 2 and #") failed, and asking a model to remove them destroyed the blank. It now flags bold, emphasis, code and headings only. This also clears the four NCERT Chapters that had failed on it (`aemr108`, `cesa106`, `fepr105`, `iebe102`); NCERT now passes 470 of 471, the last (`jesc103`) a summary two words over its limit.

## 2026-10-06: Chapter summaries for SSC (issue #99)

### D71. Chapter summaries are written in a Claude Code session, not through the Batch API
D46 planned Chapter summaries as one offline Batch API run, and #82 as a Haiku script in `lantern-agents`. The 471 NCERT summaries were in fact written in Claude Code (their `model` is `claude-opus-5-5`), and the founder asked for the same for SSC. A summary needs only the refined Chapter (its title and Concepts), not the book text, so all 207 SSC Chapters fit in one session: about 80k tokens read and 207 summaries of 43 words on average, at no API spend. `scripts/write_summaries.py` takes the written text as a JSON map and writes `summaries/<book>/<chapter>.json` (`summary-v1`) only when a summary has 20–70 plain words and its Chapter has a refined file; an empty or failing one is reported and never written, and an existing one is kept unless `--force`. The API route stays the fallback for a corpus too large for a session.

## 2026-10-05: Start screen (#113)

Numbered D69 to D73 on the #113 branch; renumbered D72 to D76 when merged after the SSC decisions D69 to D71.

### D72. The app uses native Firebase (`@react-native-firebase`), not the JavaScript SDK; Expo Go is no longer used (issue #113)
The Firebase SDK choice left open by #105 and D68. The JavaScript SDK keeps its session through `getReactNativePersistence(AsyncStorage)`, which would put the tokens in AsyncStorage and break the storage rule that tokens live only in the Firebase SDK's own store or `expo-secure-store`; a SecureStore adapter would work but is code Lantern would own and keep correct. The native SDK keeps the session in Firebase's own Android store, renews the ID token itself, and is the module D68's Analytics and Performance Monitoring need. Google's account sheet comes from `@react-native-google-signin/google-signin`, whose ID token is exchanged for a Firebase session. Cost: the app has native code, so Expo Go no longer runs it and every run builds with `expo run:android` (JDK 17 and the Android SDK), and the release build needs `google-services.json`, which is git-ignored and reaches CI as the `GOOGLE_SERVICES_JSON_BASE64` secret on `uat`. Only `src/shared/session` imports Firebase and only `src/auth` imports the Google module; lint enforces both. Considered and rejected: the JavaScript SDK with an AsyncStorage store (breaks the rule), and the JavaScript SDK with a SecureStore store (own code, no Analytics path).

### D73. A debug build can point at the local Docker stack (issue #113)
For debugging in Android Studio on the emulator, the app can use `https://10.0.2.2` (the emulator's name for the host) and the Firebase Auth Emulator (`EXPO_PUBLIC_FIREBASE_AUTH_EMULATOR`). The local CA's name constraint now permits `10.0.2.2/32` next to `local.lantern.api` (still local-only; the CA key is discarded after issuing). A config plugin copies the CA into the debug source set only, so a release build neither contains it nor trusts it, and `config.ts` refuses both `http://` and the emulator setting in a release build. Google sign-in against the emulator creates a new emulator user, so the seeded `dev-parent-*` Parents are not reachable from the phone; registration is. Rejected: editing the emulator's hosts file (not possible on Google Play images, which Google sign-in needs) and a certificate for `localhost` (a broader name for the same job).

## 2026-10-08: Start screen rebrand and Opening intro (#113)

### D74. Rebrand: teal, a mother-and-child mark, and Start copy that speaks to the Parent (issue #113)
Supersedes the colours and the lantern mascot of D62's look; the screen layouts stay. The founder wants the Parent to be the hero: Lantern does the preparation, the Parent teaches. The accent moves from blue `#2563EB` to teal `#0F766E` (white text 5.5:1), the ground to `#F2FAF9`, the ink to `#1E293B` (softer than near-black for long reading), and success to green `#15803D` so it no longer looks like the accent (on the canvas now; the app adds the token with its first success state, Check). The lantern mascot gives way to a mark of a mother teaching her child from an open book, with a path of light rising to a star; it is the launcher icon (white adaptive icon, one-colour themed icon), the splash and the in-app mark. Start reads "Teach your child with confidence." with three features in the Parent's words: Teach a chapter, Answer their questions, See their progress; "Free and non-commercial." sits under the button. Considered and rejected: keeping blue (the default of countless apps), saffron (too close to the amber light), plum (less fitting for a learning tool); an abstract mark (the founder wanted people, clearly a mother and a child). Canvas: the Android app POC boards were rebranded the same day.

### D75. The Opening screen's intro is built in #113; its routing stays in #114 (issue #113)
On launch the mark animates in while the saved sign-in is restored: the book opens, the mother and child appear, their hands reach the page, the light rises to the star, then the wordmark (about 2.75 s). It plays in full on a new install and after sign out, and in about 2 s on later launches, the same timeline slowed evenly (founder: 0.55 s felt too short), from a non-personal flag `lantern.v1.introSeen` in the storage module that `clearAll()` wipes; a phone set to less motion gets the still mark and no delay. If restoring outlasts the intro, an open book keeps turning a page. The intro uses React Native's core `Animated` on the native driver (opacity and transform only), so it needs no new dependency. `expo-splash-screen` shows the mark at launch. #114 keeps Home, "Registration comes next" and "Can't reach Lantern". Considered and rejected: the full intro on every launch (2.75 s slower every time) and stopping the intro as soon as the session is restored (usually cut off after a second).

### D76. Every word the Parent sees lives in a typed dictionary (issue #113)
App text moves to `src/shared/i18n/en.ts`, read with `useStrings()`. A Gujarati or Hindi file is a copy typed as `Strings`, so a missing key fails the build, which suits translating with AI; the Parent's Language setting (GLOSSARY) will pick the dictionary in one place, while the pilot stays English (D44). No library: the app needs neither plurals nor formatting yet. A lint rule rejects text written in JSX or label props. Considered and rejected: i18next with react-i18next (two dependencies and typed-key setup for features not yet needed).

## 2026-10-08: The API stores locked values (#123, #100)

### D77. The API holds no Family key; the Board is chosen at registration (issues #123 and #100)
Carries out D66 in the API. `POST /v1/register`, `POST /v1/family/children` and `PUT /v1/family/children/{id}` take the personal fields as locked values (`NameLocked`, `EmailLocked`, `BirthYearLocked`, `SchoolLocked`) and the register call takes the Family id the phone made and the two wrapped Family keys with their salts; the API stores them as sent and `GET /v1/me` returns them. Key Vault, `IKeyVaultClient`, the per-request key ring and the `[Encrypted]` columns are gone; birth-year range checks move to the app. A taken Family id is refused (`family-id-taken`). The Board (`cbse` or `ssc`) is part of #123's PR rather than a separate one because the registration screens need both and it is a small addition: the founder chose that, and chose no default, so a register call without a Board is refused; a Family row stored without one reads as CBSE. An SSC Child above Class 5 is refused (`class-not-available`). ADR-0010 records the scheme.

### D78. One exception type, every enum in `Constants`, nullable reference types off (PR #135 review)
Founder review of #123. (1) Every caller-facing failure is one `LanternException` carrying a `LanternErrorCode`; the ten exception classes are gone, and a new condition is a new code (supersedes the "one exception per condition" entry in the code glossary). (2) Every enum is a constant and lives in `Lantern.Core.Constants`, including `ActionType` and `ChildStatus`. (3) `Nullable` is off for every project, so models carry no `= string.Empty` defaults and no `string?`; a missing value is checked where it is used, and the API's `[Required]` attributes are explicit. (4) The locked fields keep their plain names. (5) `BoardType` starts at 1 so an unset Board is never CBSE, and a Child's School is never null (the phone locks an empty one). (6) A pre-commit hook (Husky) builds, checks formatting and unused usings, and runs the unit tests, so CI failures show up before a push.

## 2026-10-10: App working cycle

### D79. Every app feature ends with a Verification report and a code review (issue #114)
Step 5b of `docs/agents/app-workflow.md`: after the checks, the agent runs the scenarios on the emulator against local Docker, writes a table of each scenario with its result, and maps every acceptance criterion of the ticket to the scenarios that prove it, so the founder sees what was proven and what is left for a device. Step 8b runs `/code-review` before the founder's review. Why: #114 could not be tried on UAT (no registered Family), and the local run found a real flaw (a stale sign-in stuck on "Can't reach Lantern") that the Jest tests did not. Step 5b also has the agent re-read its own tests after the real run and check each assumption against it; on #114 this found that a revoked sign-in (`auth/invalid-refresh`) was wrongly treated as "can't reach", and that the fakes never failed a token refresh.

## 2026-10-10: Styling (issue #138)

### D80. The app is styled with NativeWind v5 class names and one token file (issue #138)
Screens use Tailwind class names through NativeWind 5.0.0-rc.0 (pinned exactly), with every colour, font, radius and text size defined once in `client/lantern-android/global.css`; `StyleSheet.create` and style objects are lint errors except for `Animated` values and SVG. v5 was chosen because v4's Tailwind 3 chain has five high advisories; `lightningcss` (MPL-2.0, build-time, already in the lockfile through Expo) was accepted by the founder; the library needs `react-native-reanimated` and `react-native-worklets` (the SDK 57 versions). Also new: every new library is vetted for high or critical advisories and for its licence before use (D79's report lists it). Written up in ADR-0011.

### D81. Components and screen states are reviewed in Storybook on the device (issue #139)
`@storybook/react-native` 10.6.0 shows every shared component and every screen state from fixed data (`npm run start:storybook`). A screen that reads a hook is split into a container and a view that takes its state as props. A component file needs a story or a written reason it has none, and a Jest test enforces it. Release builds carry none of it: Metro swaps Storybook for empty modules unless `EXPO_PUBLIC_STORYBOOK_ENABLED=true`, and `config.ts` refuses a release build with it on. Telemetry is off and Storybook stores nothing on the device. The experimental Storybook MCP endpoint (`experimental_mcp`) lets the agent list existing components before building a new one. See ADR-0012.

## 2026-10-11: The API reads the catalogs (issue #112)

### D82. One shared Blob role, container names as settings, no cache lifetime (amends #112)
The API identity already holds Storage Blob Data Contributor on the resource group (`bootstrap.sh`), so it reads `ncert`, `ssc` and any container added later with no further role: the API owns the infra, and a per-container role would add no restriction. The container names reach the API as `Catalog__NcertContainer` and `Catalog__SscContainer` from `containers` in the bicepparam (D36); the options class has no defaults and validates on start. The founder expects NCERT and SSC to stay the only Boards, and their content changes once a year, so there is no catalog cache lifetime setting: the API keeps a catalog in memory for the life of the process and a yearly upload is followed by a restart (a new revision). #112's acceptance criteria that name a role assignment by Bicep and a cache lifetime are replaced by this entry.

### D83. In-process test settings live in `appsettings.Testing.json`, not in `LanternApiFactory` (amends the Setting recipe)
The founder asked that no value be written in test code, as D36 asks for the apps. The fixed settings the in-process tests run with (tables, containers, resend delay, rate limit, security key) are now in `tests/lantern-api/Lantern.Api.Tests/appsettings.Testing.json`, loaded by `LanternApiFactory` and read by the two tests that need a value through `TestSettings`. Only what a run decides stays in the factory: the Azurite connection string, the Firebase project id of the test tokens and the fake Service Bus settings. A test that blanks a setting adds an in-memory source after the factory's. Step 5 of the Setting recipe in `docs/architecture/recipes.md` now says so.

## 2026-10-11: Registration step 1 (issue #118)

### D84. About you keeps the draft in memory and lets the Parent change Google account (issue #118)
The four registration steps share one in-memory draft (Parent name and email from the Google token, Region, the notice agreement). Nothing is stored on the device before Finish; closing the app or signing out drops it. A Parent who signed in with the wrong Google account taps "Not you? Use a different account" (or the back arrow), which ends the Lantern session and forgets the Google account, so the account sheet opens again. The Firebase user made by the wrong sign-in stays in Firebase Authentication with no Lantern record, because `/v1/register` runs only at Finish. Android's own Back on About you is left to the system and closes the app, with the session kept. The canvas board 3 shows the link.

### D85. The app is driven with Maestro, on an Android 34 emulator (issue #118)
Verifying a screen by reading screenshots cost many tokens, and `adb uiautomator` failed during animations. Maestro 2.10.0 (Apache-2.0, dev-only, never in the app, no published advisories, pinned and checked against the release's SHA-256) reads the screen as text and asserts on it: `maestro test .maestro`. The emulator moved from Android 37 to 34 because Maestro lists 29 to 34 as supported and the 37 image and AVD took 13 GB. `scripts/verify-env.sh` starts the emulator with software rendering after the host GPU path left it dead mid-verify.

### D86. The Board cards show each Board's own logo (issue #119)
The ticket left open whether Lantern may show the Maharashtra State Board's logo (ADR-0009: content only from its rights holder). The founder chose to show the real logos of both Boards on the Board screen, so the plain-icon stand-in is gone. The Maharashtra logo is the image on the canvas, taken from Wikipedia (the Maharashtra State Board of Secondary and Higher Secondary Education, Pune). The CBSE logo is the official emblem on Wikimedia Commons, "Logo of the Central Board of Secondary Education", published by CBSE under the Government Open Data License - India. Both sit in `client/lantern-android/assets/boards/`, are decorative (the card's text names the Board) and say nothing about an endorsement: Lantern is not affiliated with either Board. If either Board asks for its logo to be removed, it is replaced by a plain icon.

### D87. How the phone makes and wraps the Family key: Argon2id and AES-GCM from a native library (issue #122)
Carries out D66 on the phone. The founder agreed the dependency and the settings: `react-native-quick-crypto` 1.1.8 with its two peers (MIT, no new `npm audit` advisory), Argon2id at 64 MiB, 3 passes and 1 lane, and AES-256-GCM. The format and the reasoning are in ADR-0010's follow-up. On the Lantern_34 emulator the real library returned the RFC 9106 Argon2id test vector and took about 160 ms per derivation (two per registration); how long it takes on a low-end phone is not measured, so the founder is asked to try it on one. Decisions on the screens:
- **Two ways to keep the Recovery code, as the ticket says, not the canvas's single "Share or copy code".** "Email it to myself" opens the phone's mail app with a message to the Google address (the Parent sends it, so it never passes through Lantern), and "Save elsewhere" opens the share sheet, which is also the fallback when no mail app is set up. Canvas board 5 needs the two buttons and the busy state ("Creating…") added.
- **No Back once the code is shown**, and Android's back button does nothing there, so a Parent cannot leave without the code. Back from First child returns to the same code; it is not made again.
- **The Family key is stored when registration succeeds (#120)**, not when the Passphrase is created, so a half-finished registration leaves nothing on the phone.
- **Code review (#122), decided not to change:** the two wrappings run one after the other, not together, so the peak is 64 MiB rather than 128 MiB on low-memory phones (the cost is time, which the founder is to measure). The Recovery code screen has no `FLAG_SECURE`: it needs `expo-screen-capture`, a new library, so it is for the founder to decide. The step throws if reached without keys, which only a reordered flow or a hot reload can do.

## 2026-10-12: Each Child's Books live in the Family's own storage (issue #147)

### D88. A Family is a self-contained space; each Child's Class folder holds a Content slice (issue #147, ADR-0013)
The founder asked that registering or adding a Child copy only that Child's Board and Class Books out of the shared `ncert` or `ssc` container, so a Family reads nothing shared. Reads of read-only files cannot race, so the reasons recorded are blast radius, a frozen year, a complete delete and independence, not performance. Supersedes D82 for a Child's reads; D50's one cached prompt prefix per Board still holds because the copy is byte for byte. Decisions: the slice is `content/` in the Class folder (`family/{familyId}/{childId}/{class}/content/`), JSON only (the founder: Lantern has no picture questions); one `create-workspace` action per Child does the folder and the slice, so a registration with several Children copies all of them at once; the slice is ready when `content/version.json`, written last, exists, and Plan will refuse with `content-not-ready` and the app will show a loading state, not an error; the queue's five-minute retry stays; a slice is pinned and a founder-run `ncert-build refresh --board` will refresh stale ones later (not built: the refresh and a master `version.json` are a follow-up); the yearly upload runs in a maintenance window; a Child's Class is fixed, so moving a Child to a new Class is not built. Considered and rejected: a copy per Family or per Board, a fallback to the shared container, auto-refresh on upload, a ready flag on the Child row. Added a **Content slice** term to the glossary.

### D89. The two Boards have their own copy rules, taken from UAT's containers (issue #147)
Read from `lanternuat`: NCERT lists 1,091 Books in 24 mediums and builds 45 (Class 3: 3 of 97 entries), and has no `board.json`, `toc` or `guidance`; SSC lists the 16 it built and has all of them. So NCERT's Books are the catalog entries of the Class that have refined Chapters, SSC's all entries of the Class; SSC additionally requires `board.json` and a `toc` and `guidance` per Book; both require a `summaries` file for every refined Chapter. A missing required file fails the copy. The slice is what the master holds, not the catalog's declared range: NCERT `jewe2` (Class 10) declares 9 Chapters and `jewe204` and `jewe209` are not built, which is a content gap to close in the master, not in the copy. Class 3 is 78 blobs (about 403 KB) for NCERT and 97 (about 646 KB) for SSC.

### D90. The copy takes no lock; it checks the master did not change (issue #147)
The founder asked for care with concurrency, since many Families register together. The shared containers are only read, so no lease: a lease would serialise Families on one writer. After the copy the manifest (every blob read, with its ETag) is read again and must match, or the slice is not marked ready and the queue retries; a test with the master changing mid-copy proved a per-read `If-Match` redundant, so it was dropped. At most 32 Blob requests are in flight per process, over the host's 4 messages at a time. Each file is read whole and written with one Put Blob, because a streamed upload in blocks failed with `InvalidBlockList` when the same message ran twice at once (found by a test). The tables need no change: the handler adds two point reads, and the ledger rows are one per Child.

## 2026-10-13: First child and registering the Family (issue #120)

### D91. All Children go in the one `POST /v1/register`; there is no per-Child call from registration (issue #120)
The First child step collects Child one to six on one screen and sends them all in the single register call, so a Family is never half made and the app has no partial-failure state to show. An extra Child the Parent opened and left empty is dropped; one that is half filled blocks Finish with a plain message. The API's `POST /v1/family/children` stays for adding a Child later from Home.

### D92. Dropdowns use `@react-native-picker/picker` (issue #120)
The birth year, and the Class of an extra Child (to keep its card short), are native Android dropdowns, so the phone shows its own list. The first Child's Class is a row of numbered buttons, which is quicker to tap. MIT licence, no advisory, supports Android and iOS.

### D93. The Passphrase stays in memory so a taken Family id is retried without asking again (issue #120)
`family-id-taken` makes the phone choose a new Family id, wrap the same Family key and Recovery code again under it (the wrapped keys depend on the id) and send once more. The Parent sees nothing and the code they saved still works. This needs the Passphrase in the registration draft until Finish, kept in memory only and never stored. Any other failure, or a second taken id, is shown with Try again.

### D94. Registration tells the app it is done instead of asking `/v1/me` again (issue #120)
After a successful register the screen calls `markRegistered`, so Home shows at once. The Family key is written to secure storage only then (D87). If this phone gets `already-registered` it shows Home and keeps no key: the Family exists, but this phone did not make it, or its answer was lost and the retry found it. The Passphrase opens the Family on the next start. A way to tell those two cases apart is not built.

### D95. A failed register says what the Parent can do, not which Child (issue #120)
`class-not-available` cannot happen for a Class the app offered, since the app's Class choices equal the API's, so it gets one plain message (check each Child's Class) rather than naming a Child as the ticket asked. Offline and timeout say "Couldn't reach Lantern"; anything else says the Family was not set up. The privacy notice version sent is a placeholder (`draft-2026-10`) until the notice text is final.


## 2026-10-16: Subjects, Board profiles and the Family manifest (issue #83)

Checked against the real data first: the UAT `ncert` and `ssc` masters were loaded into the local Docker stack, two Families (CBSE Classes 3, 6 and 10; SSC Classes 1, 3 and 5) were registered through the real `POST /v1/register`, and the real `Lantern.Functions` copy made six slices. ADR-0014 has the findings and the diagrams.

### D96. A Child's Subjects come from the Child's own storage, never the shared catalog (issue #83)
D88 already said the API no longer serves a Child from the shared containers; #83 and #115 still said "the catalog of the Family's Board". They now read the Family manifest (D98) and the Child's slice. #112's Board-to-container mapping and D82's in-memory catalog are not used for a Child's reads. The route stays `GET /v1/children/{childId}/subjects` as the tickets and #103 say, with the Chapters route under it; the founder chose that over `/v1/family/children/...`.

### D97. The Board's rules run once at build time, in `ncert-build` (issue #83)
The founder asked for a rule set per Board that ends in one unified JSON for the app. Both Boards now get a `board.json` in their master (SSC's moves to `ssc-v2`, NCERT's is new, `cbse-v1`) with an ordered `subjects` list (name and icon) and `chapter_titles` (`refined` or `toc`). The build uses it to write a Class manifest per Board and Class (`manifest/class-N.json`), which the existing byte-for-byte copy puts in each slice as `content/manifest.json`. The API never reads `board.json`. A third Board is a profile and a build run. NCERT's slice still gets no `board.json`: only its master has one, so D89's line stands for slices. Amends D89 by one required copy file for both Boards.

### D98. The Family manifest is a derived index written at the end of `create-workspace` (issue #83)
`family/{familyId}/manifest.json` lists, for each Child whose slice is ready, the Child's Class and Subjects. Only `Lantern.Functions` writes it: created with `If-None-Match: *`, updated with `If-Match` and a retry, an identical entry is not rewritten, the remove handler takes an entry out, a Family delete sweeps it. It changes only when a Child is added or removed. The founder's reading, a flight manifest the crew reads instead of asking each passenger, holds because it is derived: `version.json` stays the readiness truth (ADR-0013). Registration queues one message per Child and four run at once, so the guarded write is needed; the founder first assumed one call meant one writer, and the code (`FamilyService.RegisterAsync`) shows one message per Child. Options considered: one action per registration (reverses D88), Service Bus sessions (a new queue), a manifest in each slice only (no shared index). ADR-0014.

### D99. No manifest entry means `409 content-not-ready`; Home waits per Child (issue #83)
Registration returns before the slices exist, so the first Home open often finds no entry. The API answers `409 content-not-ready` (a state conflict, like the other 409s; a 503 would feed the failure alarms for normal onboarding). Each Child becomes ready alone, so Home shows the active Child's Subjects as soon as that Child's entry exists and a loading state for a sibling still copying. Another Family's Child, an unknown Child and a Child being deleted are all `404 child-not-found`.

### D100. Separate lean controllers and a paged response (issue #83)
`SubjectsController` and `ChaptersController` stand apart from `ChildrenController`, return light models, and page from the start: `?limit=` (default 50, at most 100) and an opaque `?cursor=`, answering `{ items, nextCursor }`. A bad cursor or limit is a 400 problem response. A Class has three to six Subjects, so Home takes one page; the shape is for the Chapters list and later ones. The calls reuse the `children` rate-limit policy, and nothing is cached: the answer is one small blob.

### D101. What a Subject is, from the real data (issue #83)
A Subject groups the Books with one `subject` name and its count is the number of built Chapters, not the catalog's declared range (NCERT `jewe2`: 7 of 9). Its id is the slug of its name. Order and icon come from the Board profile, so the canvas's order is data and not code: Mathematics, English, Science, Social Science, The World Around Us, Environmental Studies; icons are `maths`, `english`, `science`, `evs` and `book` for the rest (Social Science until the canvas draws it). NCERT Classes 4 and 5 show both EVS-type Subjects as the content holds them; merging them is a content decision. Every Board says "Chapters": SSC's "unit" and "lesson" stay a build detail. The API decides what the phone shows (name, icon, count, order) and the phone only draws it.

### D102. No migration, and the UAT upload stays with the founder (issue #83)
Nothing is in production, so a Family made before this change, with no `content/manifest.json` and no manifest entry, is registered again. The dev seeder goes through the same manifest step as the handler. The build output lives only in UAT (`~/Work/lantern-data` does not exist on this machine), so the manifest stage runs on a data directory restored from UAT; the agent prepares the files and tests and the founder runs `ncert-build upload`.

### D103. A Chapter's number comes from its id on both Boards; SSC's contents file gives only the title (issue #151)
The first real run of the manifest stage on the restored UAT data failed two SSC Classes: the contents file's `number` is the label as printed, `"1"` in most Books but `"s1"` in Class 3 Maths and `"No. 1"` in Class 5 EVS. The Chapter id (`ssc3-maths-04`, `cesa101`) is always the position, so the number is the digits after the Book's id for both Boards, and the toc supplies the title only, as the Board profile says. Checked on the real masters: every Class's `chapterCount` total equals its raw count of summary files, Class 3 is CBSE 14/12/12 and SSC 14/4/25, Class 10 English is 25 with `jewe2` at 7. The `manifest` stage also writes the current `board.json` before it reads it, so data restored from the container (SSC's old `ssc-v1`, NCERT's none) is never trusted over the code.

### D104. All JSON goes through `IJsonService`, into the class that has its shape
The user asked for one JSON service across the app and for JSON to be treated as a data source: deserialize into a class, change the object, serialize it back. `IJsonService` (Core) holds the one set of options, the same as the queue and manifests already used, so no document changes on the wire. `ClassManifest` and `FamilyManifest` lost their own parsing and their copy-on-write methods: the store reads the manifest, calls `Add` or `Remove` on it and writes it back under the ETag. `version.json` became a class (`ContentVersion`). An architecture test fails when production code calls the serializer directly. The Board's catalog stays a `JsonArray` because it is copied as the publisher wrote it, but is read and written through the service.

### D105. Workspace and Board file names live in one `ContentLayout`, not as literals in the store
The user does not want string literals such as `MarkerName` in the code. The names stay in code and out of configuration, because they are a contract with `ncert-build` and with what is already stored, and D36 keeps storage formats out of settings. `ContentLayout` (Core) holds every file and folder name of the Workspace and the Board masters, builds each path from them, and carries the two manifest schema ids and the two catalog field names. It is injected, so `WorkspaceStore` has no name literals, and an architecture test fails when production code writes one outside it. Tests keep the real names as literals to pin the contract.

### D106. Home loads every Child's Subjects up front, and an unknown icon is a book (issue #116)
The Child switcher has to show a sibling whose content is still being made as loading while the active Child is already shown, and only a Subjects call can say whether a slice is ready. So Home asks `GET /v1/children/{childId}/subjects` for each of the one to six Children in parallel (each a small manifest read), keeps the answers in memory, and switching a Child is instant and only stores the Child's id. The first real run against local Docker showed that the Board profiles send `book` (Social Science), so a Subject icon the app does not know falls back to `book` instead of rejecting the whole list: the API decides what is shown, and the phone must not fail on a new icon. Home opens the Subject and Add a child routes as stubs until #117 and #121, and a phone with no Family key shows an "Unlock your Family" notice with Sign out until #136.


### D107. Local Docker can run on the real UAT masters, opt-in (issue #116)
The synthetic masters in `LocalBoardMasters` (two Subjects with one Chapter for SSC, one Subject with two Chapters for CBSE) hid the real shape of the data: a Class 3 SSC Child showed Mathematics 1 and English 1 where UAT has Mathematics 14, English 4 and Environmental Studies 25, and they missed the Social Science `book` icon. `deploy/local/load-uat-masters.sh` now empties the local Azurite and copies the `ncert` and `ssc` containers whole from the UAT storage account (read-only, `az login`, about 2 GB, kept outside the repo), and `scripts/verify-env.sh up` starts the stack with `LANTERN_SEED_BOARD_MASTERS=false` so the synthetic seeder does not overwrite them. The synthetic seeder stays the default because the `e2e-docker` CI job has no Azure access; retiring it needs a CI source for the masters, which is a separate decision.

### D108. The look of the app is three saved words read as CSS variables at runtime (issue #133)
The Appearance screen lets the Parent pick a Theme (Match phone, Light, Dark), a Text size (Normal, Large, Largest: 1, 1.15 and 1.3 times the text sizes) and a Colour (Teal, Blue, Gray, Golden), all from canvas board 10. They are stored on the phone as `theme`, `textSize` and `colourScheme` in plain storage (non-personal; the founder approved the three new values; Sign out keeps them, D109). NativeWind 5 resolves a `@theme` colour at build time, so a runtime override has no effect; tried on the emulator and confirmed. What works is `@theme inline { --color-primary: var(--app-primary, #0f766e) }` in `global.css` (the fallback is the default look, a test keeps it equal to `palettes.ts`) and a `VariableContextProvider` that sets `--app-*`. Editing `global.css` needs a Metro restart with `--clear`. Colours that code needs as values (SVG, the loading spinner, placeholders) come from `useColors()`. A new `accent` token is the colour for text and icons on the ground: the scheme colour in the light theme, a light tint of it in the dark theme (the plain colour is too dark to read on `#0E1716`); `bg-primary` stays the fill behind white text, checked to 4.5:1 for all four schemes. The mark and the Google button keep their own colours. `app.json` `userInterfaceStyle` goes from `light` to `automatic` so Match phone can see the phone's dark mode; that is a native change, so the dev client and the APK are rebuilt once. Text sizes are scaled in code from 13, 15, 16, 17 and 28 px; the wordmark and the 40 px display size are not scaled. Not done: Gujarati and Hindi, the dark look of screens the canvas does not draw (checked by screenshot), and following the Parent's account across phones.

### D109. Sign out keeps this phone's state; the Family key is kept per account (issues #133, #136)
The founder decided that signing out and in again must land a registered Parent on the same Home, with the same settings, and that only deleting and reinstalling the app makes a new phone. `endSession()` now only ends the Firebase session (also for a revoked sign-in); it no longer calls `clearAll()`, so the Family key, Appearance, active Child, welcome banner and intro flag all stay. This replaces the earlier rule that Sign out clears everything (expo-app-standards, local storage rule 4, and the Family key row) and the ticket #136 wording "after Sign out". The Family key is stored as one secure value `familyKeys`, a map from the Firebase uid to the key, read through `readFamilyKey(uid)` and written by `keepFamilyKey(uid, key)` (`src/shared/storage/familyKeyStore.ts`), so a second Google account on the same phone gets its own key and never reads the first Family's names. `clearAll()` stays for Family delete, Child delete and Start again. Android backup is off, so a reinstall or a new phone has no key and sees Unlock your Family (#136), which now means only those two cases. Risks accepted: anyone who can open the phone and signs in with a Google account that has a Family on it reaches that Family on this phone without a Passphrase, as long as the phone holds the key; the key is still in the Keystore. The old single `familyKey` value is no longer read, so a test phone that registered before this change shows Unlock once. Also in this change: the logo follows the colour scheme and theme.

### D110. Theme is Light or Dark, with no Match phone (issues #133, #157)
The founder asked, during the UAT test on a real phone, to remove Match phone from Settings › Appearance and keep only Light and Dark. This replaces the three Theme choices on canvas board 10 and in D108. The default is Light. A saved `theme` of `system` from the earlier build is not one of the two values, so the storage rule for an unknown value applies and it reads as Light; nothing else migrates. The phone's own dark mode no longer changes the look. `app.json` keeps `userInterfaceStyle: automatic` so the keyboard and system dialogs still follow the phone; the app's colours follow the Parent's choice only.

### D111. The phone's text size and the app's Text size together are held to 1.4 times the design (issue #162)
Android multiplies every font by the phone's text size after the app's own value. On the OPPO test phone set to 1.15, the app's Largest (1.3) reached about 1.5 times the design and the screens crowded or hid controls. `scaledPx` now takes the phone's font scale and passes React Native a size that, after React Native's own multiplication, equals the design size times the smaller of the two scales multiplied and 1.4. Nothing changes below the limit: Normal on a 1.15 phone is still 1.15, Large is 1.32. Above it the text stops growing, so a phone set to the largest system size gives 1.4. The 1.4 is one constant (`MAX_TEXT_MULTIPLIER`) to tune after more phones are tried; the wordmark and the 40 px display size are still not scaled.

### D112. Unlock your Family is its own screen, with a wait after wrong tries (issue #136)
A registered Parent whose phone holds no Family key (a new phone or a reinstall, D109) opens the Family on the Unlock screen, `app/unlock.tsx`. Home keeps its locked notice, which now has an Unlock your Family button beside Sign out, and Settings shows an Unlock your Family row only while the phone has no key. Both doors open the same route; a success goes to Home, which asks again when it regains focus. The Parent picks the Passphrase or the Recovery code, the phone fetches the wrapped keys and salts once with `GET /v1/me`, derives the key and opens the Family on the phone, so a typed value is never sent, stored or logged and a wrong value makes no call. The only new stored value is `unlockWait`, a count of wrong tries and the time the next try is allowed, in plain storage (non-personal): the wait is 5 seconds after the first wrong try, doubling to 5 minutes, kept across app restarts so closing the app does not reset it, enforced in `unlockFamily` before any key is derived (not only by the disabled button), and cleared on success and by `clearAll()`. Start again asks for a confirmation, calls `DELETE /v1/family`, then `clearAll()` as D109 says, and asks the API who the Parent is again so the app lands on About you. The wrong-value message sits above the button, outside the scroll area, because the keyboard hid it on a phone. The tests in `src/unlock` assert the security rules: the only requests are `/v1/me` and `/v1/family`, nothing typed or derived appears in a request, storage or log, and a try during the wait does no key work. The Start again screen uses the existing danger outline button for Delete and the primary button for Keep, which differs from canvas board 11's draft (a filled red Delete); the board is updated to match.

### D113. The Passphrase is dropped; the emailed Recovery code is the only key (issues #136, #165, #166; ADR-0015)
The founder decided, while building Unlock your Family, that a Passphrase a Parent can forget is the wrong lock, and that the Recovery code should be sent to the Parent's own email so it is always in their inbox. This supersedes D66 and the two-wrapped-copies part of ADR-0010 and the Passphrase screen of #122. The phone wraps the Family key once, with a key derived from the Recovery code; the Parent must open the email to themself (the phone's mail app sends it, Lantern never does) before registration continues, and the phone cannot confirm it was sent, so the step waits for the Parent's tick. Registration keeps four steps, with step 3 now the Recovery code (made on the phone when the step opens) and no Passphrase screen, Unlock your Family takes the Recovery code alone, and Start again moves from the Unlock screen to Settings so a Parent in a hurry is not offered a deletion next to the unlock. Accepted risk: whoever can open the Parent's email can open the Family. ADR-0015 holds the reasoning and the API change.

### D114. The security step walks abuse cases, and the device gap is recorded (issue #168)
The founder asked what a stolen Recovery code, a stolen inbox or a second phone could reach, and that children's data must not leak across accounts. The Security step (`CODING_STANDARDS.md`, both workflows) now requires a who-holds-what table for anything touching sign-in, keys or a Family's data, with a test or a recorded accepted cost for each row. ADR-0015 carries the table for the Recovery code. The gap it found, that Lantern cannot see or revoke a second phone signed in as the same Google account, is not solved here: it is the experimental issue #168 (devices list, new-device approval, delayed release, key rotation). A test now pins that a stranger's code never opens another Parent's Family. Every API controller declares `[Authorize]` itself, so a gate is visible in the file. The founder removed the fallback policy from `Program.cs` so that nothing hides a missing attribute: `AuthorizationTests` fails a controller with neither `[Authorize]` nor `[AllowAnonymous]` of its own, and `AuthenticationTests` calls every endpoint without a token and expects 401. An anonymous endpoint needs an ADR and a decision-log entry first.

### D115. A Subject's Chapters are numbered 1 to N across its Books, and Home follows a Child switched on Chapters (issue #117)
The Class manifest keeps each Chapter's number inside its Book, so Class 7 Mathematics reads `gegp1: 1, 2` and `gegp2: 1`, and it holds no Book names, only ids such as `gegp2`. The founder decided that `GET /v1/children/{childId}/subjects/{subjectId}/chapters` numbers a Subject's Chapters 1 to N in manifest order across all its Books, before paging, so the phone shows one list with no Book headings and no new data in the manifest. The cost is that a later Book's first Chapter shows as, say, 3 where the printed Book says 1; the title tells the Parent which one it is. `bookId` is not in the response. The same call reads the Child's own `content/manifest.json` only after the Subject is found in the Family manifest, so an unknown Subject is 404 `subject-not-found` and a missing file is 409 `content-not-ready`. On the Chapters screen the Child switcher from the canvas works: it saves the active Child as Home does, and Home reads it again when it regains focus, so Back shows the Child last chosen. The founder also replaced the Subject chips on canvas board 8 and in the ticket with a drop-down, the same pattern as the Child switcher: a Subject button under the header opens a bottom sheet listing the Child's Subjects with their Chapter counts, which also scales to the ten Subjects a Class can have. Chapters are not tappable until the Plan milestone.

### D116. A Chapter's Attempts and Checks live in a `progress` table; the list loads a page at a time (issue #117)
The founder wants each Chapter to be planned and verified more than once, with every Attempt and every Check kept with its marks, and the Chapters screen to say where each Chapter stands. ADR-0016 records the design: a dedicated `progress` table partitioned by the Family, with a summary row per Chapter and append-only-style Attempt and Check rows, a Chapter state that is derived from them (Not started, Planned, Taught, Checked), and one table transaction per change. It replaces D22's History partition for Chapter progress and #88's History blobs. The state is called Chapter state and the words are Planned, Taught and Checked, not Tested, because the glossary reserves Test for the school-test feature. Azure SQL was considered and deferred: Table Storage serves a Child's history in one range read, and the ADR names what would justify SQL later. The Chapters list is searched by the server (`?q=`, a name contains, at most 64 characters, case-insensitive, Chapter numbers kept) and the phone loads it 20 at a time as it is scrolled, asking for each page once, because a Subject can have more than 30 Chapters. The Chapter history is a screen of its own, paged 10 Attempts at a time. The Plan itself, the Check and Ask screens, Resume and Progress are out of scope and the write operations have no HTTP caller yet; the local Docker host seeds a sample history through `POST /dev/seed-progress` so the screens can be seen. Home stays a front door: it shows no scores or weak areas, and Progress is where evaluation will live.

### D117. The Maestro flows stay on the founder's machine, not in the repo (issue #121)
Every screen story added a flow to `client/lantern-android/.maestro/`, and each flow registers a Family from scratch, so the folder grew to 13 files that only matter while a screen is being verified. The founder decided to keep them off GitHub. Maestro is still how the app is driven (D85), and the cycle still runs the flows and reports them, but `.maestro/` is git-ignored: the files are hand-written, not generated, so they live only on the founder's machine and a fresh clone has none. The last committed copy is at commit `2be06af` (`git show 2be06af:client/lantern-android/.maestro/<name>.yaml`). A reviewer can no longer read a flow in the PR, so the Verification report names each scenario and what the flow asserted.

### D118. Usage numbers run for a registered Parent, are off from every launch, and name only what can be sent (issue #126)
Built on D68. Collection starts off at every launch, because the Firebase SDK keeps its on/off flag across launches and a Parent who ticked and then closed the app would otherwise be counted before the app decides again. It is on for a Parent whose Family exists (they ticked the notice when registering, and Consent is per Parent), and while the notice is ticked on About you. It goes off on sign-out, on an untick, and when Start again deletes the Family, until the Parent agrees again. D68 said collection starts after the tick on About you; this adds the registered Parent on a new phone or after signing in again, who never sees About you, and the strict reading would leave that Parent uncounted for good. Opening, Start and the tick itself send nothing because no one has agreed yet, so there is no `opening` or `start` screen name and no `continue-with-google`, `open-privacy-notice` or `accept-privacy-notice` tap. The first launch's app-start timing is never recorded, as it happens before any tick. Accepted cost: the SDK remembers "on" across launches, so for about three seconds after a launch (until the JavaScript runs and switches it off) a Parent who ticked but never finished registering is still switched on; the emulator log showed no event sent in that gap. The only code that calls the SDK is `src/shared/analytics`; its screen, tap and registration-step names are `as const` lists, so a free-text value does not compile, and it refuses an unlisted name at runtime. Time on a screen is whole seconds in the foreground only. The Firebase SDK keeps a random installation ID and its on/off flag on the phone in its own store, which the notice says; the app writes nothing new. The manifest blocks both advertising ID permissions, `com.google.android.gms.permission.AD_ID` and `android.permission.ACCESS_ADSERVICES_AD_ID` (Google's libraries add them), and `android/` must be generated again after `app.json` changes (`npx expo prebuild --platform android`) or a local build keeps the old manifest. Considered and rejected: counting only after the tick on About you (leaves registered Parents uncounted), and the SDK's own screen reporting (it names Android activities, not screens).

### D119. Actions are published and subscribed on CloudAMQP (LavinMQ first), not queued on Service Bus Basic (issues #173, #174)
The founder decided, in `/ideate` on 2026-10-30, that the Plan, Ask and Check operations of the Pilot must be new subscribers of an action without a dispatcher change, and that a second consumer of one action needs Service Bus Standard (about ₹880 a month against about ₹0.05 for Basic). CloudAMQP's free plans give publish and subscribe for ₹0 under 1M messages a month. The API publishes an action to one direct exchange with the action name as the routing key, the broker routes it to one durable queue per subscriber, and each subscriber is one thin trigger in `Lantern.Functions` that hands the message to a broker-free handler. The dispatcher, the handler `Type` lookup and the unknown-type path are deleted. Publishing sits behind `IPublishingService`, so a move back to Azure (Service Bus Standard topics) swaps one implementation and the triggers. This supersedes ADR-0004 and D31 for the transport and the dispatcher; ADR-0003's guarantee that a hard delete is never abandoned and ADR-0006's one trace still hold. The broker URL is the Key Vault secret `amqp-url`, which reverses ADR-0004's "no connection string exists in Azure" for this one resource, on purpose. `bootstrap.sh` creates it as a placeholder (content type `placeholder`), the founder replaces it with the real URL by hand (`infra/README.md`), and the release preflight reads the content type through Azure Resource Manager and fails while it is the placeholder. The deploy identity has Contributor only and PR code runs as it, so it is never given read access to a secret value. Accepted cost: the free plan is shared hardware meant for UAT, with no production environment yet, and Service Bus Standard is the fallback if it proves unfit; revisit at 800,000 messages a month. The founder chose, when provisioning (#174), to try CloudAMQP's free LavinMQ plan (Loyal Lemming) before RabbitMQ (Little Lemur), because CloudAMQP offers it with twice the plan limits and it speaks AMQP 0.9.1 with the RabbitMQ API, so `RabbitMQ.Client` and the Functions RabbitMQ trigger should work unchanged. This departs from the spec's RabbitMQ and is provisional: LavinMQ is not proven for the KEDA RabbitMQ scale rule (which reads the management API), the alternate exchange, per-queue dead-lettering and the definitions import, and it has no quorum queues. The spike (#175) tests all of these on LavinMQ first; if any fails, the fallback is a free Little Lemur instance, which changes the plan and the URL and nothing else. The secret keeps the broker-neutral name `amqp-url`. The spike (#175) can overturn any provisional decision in the spec. The old Service Bus namespace is deleted by hand after the cutover (#176) is verified.
