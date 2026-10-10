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

### D32. The API is layered like wf (amends D10's "modular ASP.NET" and the folder layout of #51)
The user found the API layer bloated and asked for it to mirror the layered architecture they built before (`wf`), keeping every earlier decision. Four projects: `Lantern.Api` (controllers and the API models, the only shapes the app sees), `Lantern.Core` (service models, contracts, the generic `ServiceBase<T>`, crypto, a generic `ServiceBusService`, the action dispatcher), `Lantern.Repository` (entities, `UnitOfWork<TEntity>`, the generic `BaseRepository<TModel, TEntity>`, field encryption) and `Lantern.Base` (`FamilyService`, `ChildService`, validators, action handlers). Encryption moved from the services into the repository with the stored bytes unchanged. Key Vault sits behind `IKeyVaultClient`, the one thing local runs and tests replace. Registration lives in `FamilyService`; an interactor waits until a service grows too complex. Done inside the #51 PR, as an architectural pivot of the feature in flight. See ADR-0005.

## 2026-10-02: Class spaces start in the Functions app; handlers live there; tests leave apps/

### D33. A Class space is started by `Lantern.Functions`, not inside the request (supersedes D24's ordering)
The user moved the Blob write out of the request: adding or registering a Child now records a `ClassSpaceStart` action (`class-space-start` on the queue) in the ledger before the Child rows, writes the rows, then sends it; the Functions app writes the marker. The ledger row is the commit point, as for a delete, so a crash after the rows still gets the Child its space, and a message for a Child whose add failed does nothing. The handler reads only the Child's status (no Family key, so the Functions app still needs no Key Vault access), and sweeps the space again if a delete finished while it was writing. "Every Child has a Class space" becomes "every Child gets one shortly after it is added"; anything that later writes into a space must cope with it not existing yet. Registration no longer fails when Blob storage is down. See ADR-0002.

### D34. Action handlers and the dispatcher live in `Lantern.Functions` (amends ADR-0005)
Finishing an action is the Functions app's job, so `IActionHandler`, `ActionHandler<T>`, the dispatcher and the handlers (`ChildDeleteHandler`, `ClassSpaceStartHandler`) moved from `Lantern.Core`/`Lantern.Base` into `Lantern.Functions/Handler`, registered by `AddLanternFunctions()`. `Lantern.Core` keeps the publish side the API needs (`ActionMessage`, `ActionType`, payloads, `IActionPublisher`); services and repositories stay in `Lantern.Base` and `Lantern.Repository` so both apps reuse them. The queue name is no longer a constant: Bicep passes the queue it creates as `Actions__Queue` to both apps, the publisher reads `ActionOptions`, and the trigger reads `%Actions:Queue%`.

### D35. Test projects live under `tests/`, not `apps/`
The user wants `apps/` and `libs/` to hold production code only. The four test projects moved to `tests/lantern-api/` (mirroring `apps/lantern-api/`), including `Lantern.Api.Test.Integration.Host`, the local Docker host. This departs from wf, which keeps `*-test` projects beside the apps, on purpose. The CI guardrail now greps all of `apps` and `libs` for test-only code.

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
