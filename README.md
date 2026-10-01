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

ncert.nic.in PDFs                                         Mother's phone (Flutter, text Q&A)
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
  lives in `../lantern-data` and the private `ncert` blob container. See
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
- **Privacy.** No keys anywhere: managed identity and RBAC. Personal details are kept under a key
  per family in Key Vault; deleting that key erases the family's data (DPDP Act 2023).
- **Families, Parents and Children.** A **Family** groups its **Parents** and **Children**, like a
  resource group. Each Parent has their own settings (Language, Consent); a Child is stored once,
  however many Parents the Family has. In Table Storage the `parents` table holds each Parent's
  settings and their `FamilyId`, and the `families` table holds the Family, its Parents'
  membership and its Children. The terms are defined in [`GLOSSARY.md`](GLOSSARY.md) and the
  layout is [ADR-0001](docs/adr/0001-family-and-parent-in-separate-tables.md). Each Child also has
  a Blob **Class space** per Class under `family/{familyId}/{childId}/{class}/`, started at
  registration and kept for the Child's History
  ([ADR-0002](docs/adr/0002-class-space-in-blob.md)).

| Area | Choice |
| --- | --- |
| Mobile app | Flutter (Android first) |
| Sign-in | Firebase Authentication (Google) |
| Backend | ASP.NET on .NET 10, one modular app in Azure Container Apps |
| Storage | One Azure Storage account: Blob (raw data, NCERT layer, memory files), Table (indexes, concept vectors, answers), Queue (events) |
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
- **API.** Firebase sign-in, `POST /v1/register` and `GET /v1/me` with per-family encryption are
  live in UAT. The end-to-end tests run in Docker on every PR and gate the release, and a smoke check follows each UAT deploy. The Family split
  (#48) is merged and Class spaces (#49) are built. A cache for the Family key (#52) comes next.
  Under `apps/lantern-api`: `Lantern.Api` is production code only; `Lantern.Api.Tests` holds unit and
  in-process tests; `Lantern.Api.Test.Integration` holds the end-to-end tests, which run unchanged
  against local Docker and UAT; `Lantern.Api.Test.Integration.Host` holds the local stand-ins (Firebase
  Auth Emulator tokens, a Key Vault stand-in, a seeded dev Family) and is never in the production
  image. Running it locally: [`deploy/local/README.md`](deploy/local/README.md).
- **Next.** The Ask API, to be designed, and, before production, a Family key held on the device (#47).

How we work in this repo (cycle, skills, standards) is in
[`docs/agents/workflow.md`](docs/agents/workflow.md) and [`CODING_STANDARDS.md`](CODING_STANDARDS.md).

The decisions so far are in
[`docs/ideation/decision-log.md`](docs/ideation/decision-log.md) and [`docs/adr`](docs/adr), and
the work is tracked in [Issues](../../issues) and the project board.

## License

Lantern is source-available under the [PolyForm Noncommercial License 1.0.0](LICENSE). It is free for
families, personal use, schools, charities and other non-profits. Selling it or using it commercially
needs written permission: open an issue to ask. The agent skills in `.claude/skills` are third-party
and keep their own licenses (MIT, see each skill).
