# Lantern

*Light the way, one lesson at a time.*

Lantern is a non-profit app that helps parents with limited schooling teach their children at home.
It is funded by helper groups and the project's own founder, and it shows what AI can do for
ordinary families.

## The problem

A mother, 35, studied in Gujarati medium and reads a little English. Her son, 6, is in Class 1 at a
CBSE school that teaches English, Hindi and French. Every evening she struggles to get him to focus,
explain his lessons, finish homework and answer his questions. Searching gives generic answers and
tuition is not affordable.

## What Lantern does

Lantern is built for the parent, not as a tutor that replaces her. She sets up her child once, picks
a subject and a chapter, and gets a plain explanation in her own language, a way to explain it to a
6-year-old, and a short activity to do together. Every answer is **grounded in the child's actual
books**, so the AI does not make things up for a child.

## How it is built

The diagram is [`docs/architecture/lantern-architecture.html`](docs/architecture/lantern-architecture.html)
(open it in a browser). Two halves meet only in private Blob containers:

- **Build time**, on a workstation: textbook PDFs become concepts, questions and answers through
  local models and Claude Code (D16). Data stays out of this repo
  ([`tools/ncert-build`](tools/ncert-build/README.md)).
- **Runtime**, on Azure: the Parent app talks to an ASP.NET API, and queued work runs in
  `Lantern.Functions` through Service Bus (ADR-0004).
- **Privacy.** The phone locks personal details with a Family key only the Parent can open, through
  the emailed Recovery code (ADR-0010, ADR-0015), so Lantern stores them but cannot read them.
  Terms are in [`GLOSSARY.md`](GLOSSARY.md).

| Area | Choice |
| --- | --- |
| Parent app | Expo (React Native, TypeScript), Android first ([ADR-0007](docs/adr/0007-parent-app-in-expo.md)) |
| Sign-in | Firebase Authentication (Google) |
| Backend | ASP.NET on .NET 10 in Azure Container Apps, plus Azure Functions for queued actions |
| Storage | Azure Table and Blob, Service Bus Basic |
| AI | Claude through the Anthropic API, in a Node.js agent service (ADR-0008) |
| Infrastructure | Bicep, deployed by GitHub Actions |

## Status

- **Content.** The NCERT layer is being built class by class; the SSC layer follows.
- **Infrastructure.** UAT is deployed from Bicep by `api.yml` ([`infra/README.md`](infra/README.md)). A CloudAMQP LavinMQ broker is provisioned for publish and subscribe (D119, #173); actions still use Service Bus until the cutover.
- **API.** Register, `me`, Family and Child management, Workspaces, Subjects, Chapters and a Chapter's Attempts and Checks are built, and the
  end-to-end tests gate each release. Run it locally with [`deploy/local/README.md`](deploy/local/README.md).
- **Parent app.** Sign-in, registration, Home, Add a child, Chapters (state, search, history), Unlock, Settings and anonymous usage
  numbers after Consent are built ([`client/lantern-android/README.md`](client/lantern-android/README.md)).
- **Next.** The Ask and Plan agents.

How we work (cycle, skills, standards) is in [`docs/agents/workflow.md`](docs/agents/workflow.md) and
[`CODING_STANDARDS.md`](CODING_STANDARDS.md). Decisions are in
[`docs/ideation/decision-log.md`](docs/ideation/decision-log.md) and [`docs/adr`](docs/adr); work is
tracked in [Issues](../../issues).

## Git hooks

Run `npm install` once at the repo root to install Husky. `scripts/pre-commit.sh` then builds .NET
changes with no warnings, checks `dotnet format` and runs the unit tests (they need `azurite` on the
PATH), and checks app changes with typecheck, lint, format and Jest.

## License

Source-available under the [PolyForm Noncommercial License 1.0.0](LICENSE): free for families,
schools, charities and other non-profits. Commercial use needs written permission: open an issue.
The agent skills in `.claude/skills` keep their own licenses.
