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
  holding Blob, Table Storage (not Cosmos DB) and Queue.
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
2. A worker (`BackgroundService`) handles the event with guarded handlers.
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
A Parent adds, edits (Name, School, BirthYear; never Class) and deletes Children. Add takes a client `childId`, so a repeat returns the existing Child, and an ETag-checked update of the Family row in the same batch holds the limit of 6 under concurrent adds. Delete is a hard delete done by the D14 worker pattern with no Queue: the API writes a pending-delete row, marks the Child `deleting`, returns `202`, and a `BackgroundService` finishes the Blob, History and row cleanup, retrying until done. It adds to D14 and supersedes nothing. The Answer library (the D23 anonymous store) is never deleted with a Child. Every Child call scopes to the caller's Family from the token; a foreign or unknown id is 404. A Family may end with no Children. See ADR-0003.
