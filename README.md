# Lantern

*Light the way, one lesson at a time.*

Lantern is a non-profit app that helps parents with limited schooling teach their children at home.
It is funded by helper groups and the project's own founder. It is also meant to show what AI can do
for ordinary families.

## The problem

The case study behind Lantern:

- **Mother, 35.** Studied in Gujarati medium and reads a little English. Comes from a low-income
  family and wants her son to do well.
- **Kid, 6.** In Class 1 at a CBSE school that teaches English, Hindi and now French.

Every evening she struggles to:

- get him to sit and focus;
- explain what his class is teaching;
- get homework done well;
- answer the questions he asks.

Searching on her phone gives generic answers, because she doesn't know how to ask well. Tuition is
not affordable, and even with tuition the child would still need help at home.

## What Lantern does

Lantern is built for the parent, not as a tutor that replaces her.

- She sets up her child once: age, school, class, and the language she wants help in.
- She photographs his books: the cover and the contents page.
- She picks a subject and a chapter. Lantern then gives her a plain explanation in her own language,
  a way to explain it to a 6-year-old, and a short activity to do together.

Every answer is **grounded in the child's actual books**: the same method, the same words and the
same level, so the AI does not make things up for a child.

## How it is built

The full diagram is [`docs/architecture/lantern-architecture.html`](docs/architecture/lantern-architecture.html)
(download and open it in a browser). It has two halves that meet only in Blob storage and the
concept index: a **build time** that runs once per chapter on a workstation, and a **runtime** that
serves every question from Azure.

```
BUILD TIME (workstation, never serves mothers)            RUNTIME (Azure, Central India)

ncert.nic.in PDFs                                         Mother's phone (Expo, text Q&A)
  └► ncert-build: catalog → download → extract → segment     │ Firebase sign-in, HTTPS + JWT
       └► local models (Ollama, RTX 3060, ₹0)                 ▼
            drafts: concepts + kid questions              Ask API (ASP.NET, .NET 10, Container App)
            pictures: figures read by a vision model        router: cache → chapter → all classes → defer
       └► bundle → Claude Code refinement                   Claude only on a cache miss, answer stored for reuse
            concept cards, worked answers, Q&A (EN + GU)       │
  └► upload ──────────────► Blob ncert/ + Table concept index ◄┘
```

### Build time: the pristine layer (D16)

- **Grounded in NCERT.** CBSE textbooks for Classes 1–10: 46 core English-medium books, 471
  chapters. The PDF's own text layer is used, with no OCR; Symbol-font maths and stacked fractions
  are rebuilt from the page.
- **Local models draft, Claude refines.** A local model (qwen3:8b through Ollama) drafts concepts
  and kid-style questions overnight at no cost, and a vision model reads every figure. Claude Code
  then checks each draft against the source text and writes the concept cards and answers.
- **Opus thinks once, at build time.** Every answer after that is mostly a lookup.
- **Resumable.** Every stage skips done work, so the long runs survive reboots.
- **Data stays out of this repo.** NCERT text is copyrighted and the repo is public; the output
  lives in `../lantern-data` (`../lantern-data-ssc` for the SSC Board) and the private `ncert`
  and `ssc` blob containers. See
  [`tools/ncert-build`](tools/ncert-build/README.md).

### Runtime: one question (D15)

- **The context funnel.** Navigation builds the AI's context: child → subject → book → chapter. A
  question is always asked in a narrow, grounded place.
- **Router.** Cache → the chapter's concepts → all classes (nearest class wins) → defer. The fact
  comes from the anchor concept; the answer is pitched at the child's class. Questions with no
  concept close enough are logged and become the gap list.
- **Tag and reuse.** Answers are cached by concept, stage (Classes 1–2, 3–5, 6–8, 9–10) and
  language, so each question is answered once and the cost per family keeps falling. Shared
  answers are generic; who asked stays private to the family.
- **Data is ore (D9).** Every input is stored raw and never changed. Events on a queue trigger
  workers that refine it into clean memory files for the child and the mother.
- **Bounded cost.** Scales to zero with one replica at most, fresh-answer limits per session and
  week, and a $10 monthly budget alert. Thumbs up / down tunes the router and retires bad answers.
- **Privacy.** No keys anywhere: managed identity and RBAC. Personal details are locked on the phone
  with a key only the Parent can open (the Passphrase and Recovery code, ADR-0010), so Lantern stores them but cannot read them; deleting a family erases its rows and the wrapped keys (DPDP Act 2023).
- **Families, Parents and Children.** A **Family** groups its **Parents** and **Children**, like a
  resource group. Each Parent has their own settings (Language, Consent); a Child is stored once,
  however many Parents the Family has. In Table Storage the `parents` table holds each Parent's
  settings and their `FamilyId`, and the `families` table holds the Family, its Parents'
  membership and its Children. The terms are defined in [`GLOSSARY.md`](GLOSSARY.md) and the
  layout is [ADR-0001](docs/adr/0001-family-and-parent-in-separate-tables.md). Each Child also has
  a Blob **Workspace** under `family/{familyId}/{childId}/`, with a folder per Class, created in the
  background after registration or an add and kept for the Child's History
  ([ADR-0002](docs/adr/0002-class-space-in-blob.md)). Deleting a Child is a hard delete the API
  records and `Lantern.Functions` finishes from a Service Bus queue; the anonymous Answer library is never
  touched ([ADR-0003](docs/adr/0003-deleting-a-child.md), [ADR-0004](docs/adr/0004-actions-through-service-bus-and-functions.md)).
  Deleting a Family removes its Parents' profiles at once, so they are signed out of it, and
  `Lantern.Functions` then shreds the Family key and erases its rows and Workspaces (D40).

| Area | Choice |
| --- | --- |
| Mobile app | Expo (React Native, TypeScript), Android and iOS, no web ([ADR-0007](docs/adr/0007-parent-app-in-expo.md)) |
| Sign-in | Firebase Authentication (Google) |
| Backend | ASP.NET on .NET 10, one modular app in Azure Container Apps, plus `Lantern.Functions` (Azure Functions) in a second Container App that handles queued actions from Azure Service Bus (Basic) |
| Storage | One Azure Storage account: Blob (raw data, NCERT layer, memory files), Table (indexes, concept vectors, answers, the action ledger), Queue (events) |
| Search | Exact nearest-neighbour over embeddings in memory, no vector database |
| Live updates | SignalR, running inside the app |
| AI | Claude through Microsoft Foundry, behind a task-based gateway; model per task in config (D11) |
| Build-time AI | Ollama on the workstation (qwen3:8b, qwen3-vl:8b), then Claude Code |
| Infrastructure | Bicep with `.bicepparam` files, deployed by GitHub Actions |

## Status

- **NCERT layer.** The build tool is working and the pristine layer is being built class by class
  (`tools/ncert-build`).
- **Infrastructure.** The UAT environment is deployed from Bicep by one GitHub Actions workflow (`api.yml`), together with each API release; see
  [`infra/README.md`](infra/README.md).
- **API.** Firebase sign-in, `POST /v1/register` and `GET /v1/me` store locked personal values and the Parent's wrapped Family keys (the phone makes the key, ADR-0010; #123), and a Family picks its Board, CBSE or SSC, at registration (#100). Add, edit and delete a Child (`/v1/family/children`, #51) are built: delete returns
  `202` and `Lantern.Functions` finishes it from a Service Bus queue, and the max is six Children. The end-to-end tests run in Docker on every PR and gate the release, and a smoke check follows each UAT deploy. The Family split
  (#48) is merged and Workspaces (#49, then called Class spaces) are built. Deleting a Family (`DELETE /v1/family`, #53) is built: it returns `204` with the caller already unregistered, and `Lantern.Functions` erases the rest. Moving a Child to a new Class (#50) is next, in the Family management milestone. The Family key is unwrapped once per request with no cache (#52 dropped, D40). One request is one end-to-end trace in Application Insights, through Service Bus into the Functions app (#70, [ADR-0006](docs/adr/0006-one-trace-in-application-insights.md)); the KQL to pull it is in [`infra/README.md`](infra/README.md).
  The code is layered like `wf` ([ADR-0005](docs/adr/0005-layered-like-wf.md)): under `libs`, `Lantern.Core` holds the
  service models, contracts, generic `ServiceBase<T>`, crypto and the action publisher, `Lantern.Repository` holds the
  table entities, unit of work and generic `BaseRepository` (it alone encrypts fields), and `Lantern.Base` holds the
  services. Under `apps/lantern-api`: `Lantern.Api` holds the controllers and the API models (the
  only shapes the app sees); `Lantern.Functions` is the queue-triggered Functions app with the action handlers (it
  creates and removes Workspaces from the one `workspace-events` queue). Tests live under `tests/lantern-api`: `Lantern.Api.Tests` and
  `Lantern.Functions.Tests` hold unit and in-process tests; `Lantern.Api.Test.Integration` holds the end-to-end tests, which run unchanged
  against local Docker and UAT; `Lantern.Api.Test.Integration.Host` holds the local stand-ins (Firebase
  Auth Emulator tokens, a seeded dev Family) and is never in the production
  image. Running it locally: [`deploy/local/README.md`](deploy/local/README.md).
- **Parent app.** `client/lantern-android` is the Expo app. It opens with an animated intro of the mother-and-child mark and has the Start screen (#113, teal look, D74 to D76): **Continue with Google** through native Firebase, a session the SDK keeps and renews on the device, and a placeholder for signed-in Parents that asks UAT `GET /v1/me` and has Sign out, until the registration and Home stories replace it. It has the guardrails for every later screen (#110): lint rules, one storage module (nothing personal on the device, Android backup off), one API client that sends the Firebase ID token, a Jest setup, styling with NativeWind class names and one token file (`global.css`, ADR-0011), a Storybook story for each component state, viewed on the emulator with `npm run start:storybook` and left out of release builds (ADR-0012), and every word the Parent sees in one typed dictionary (`src/shared/i18n`), ready for Gujarati and Hindi. GitHub Actions (`android.yml`) checks every PR and, on a manual run, builds a signed APK that expires after a day; the UAT address and `google-services.json` are secrets on the `uat` environment, not in the repo. A debug build can instead point at the local Docker stack. Run and build steps are in [`client/lantern-android/README.md`](client/lantern-android/README.md).
- **Next.** The Ask API, to be designed, and the registration screens in the app that use the locked-value API.

How we work in this repo (cycle, skills, standards) is in
[`docs/agents/workflow.md`](docs/agents/workflow.md) and [`CODING_STANDARDS.md`](CODING_STANDARDS.md).

The decisions so far are in
[`docs/ideation/decision-log.md`](docs/ideation/decision-log.md) and [`docs/adr`](docs/adr), and
the work is tracked in [Issues](../../issues) and the project board.

## Git hooks

After cloning, run `npm install` once at the repo root: it installs Husky, which runs `scripts/pre-commit.sh` before every
commit. A commit that touches .NET code must build with no warnings (unused usings included), match `dotnet format`, and
pass the unit tests (they need `azurite` on the PATH: `npm install -g azurite@3`); a commit that touches the app must pass
typecheck, lint, format and the Jest tests. The E2E tests stay in CI. `git commit --no-verify` skips the hook.

## License

Lantern is source-available under the [PolyForm Noncommercial License 1.0.0](LICENSE). It is free for
families, personal use, schools, charities and other non-profits. Selling it or using it commercially
needs written permission: open an issue to ask. The agent skills in `.claude/skills` are third-party
and keep their own licenses (MIT, see each skill).
