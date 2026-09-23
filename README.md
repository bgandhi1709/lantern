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

- **Data is ore.** Every input is stored raw and never changed. Events then trigger workers that
  refine it into clean memory files for the child and the mother.
- **The context funnel.** Navigation builds the AI's context: child → subject → book → chapter. A
  question is always asked in a narrow, grounded place.
- **Tag and reuse.** Children ask the same thing in many ways. Each question is matched against
  others at the same funnel node and answered once, which keeps the cost per family falling.
- **Privacy.** Personal details are kept in an encrypted identity vault with a key per family.
  Deleting that key erases the family's data (DPDP Act 2023).

| Area | Choice |
| --- | --- |
| Mobile app | Flutter (Android first) |
| Sign-in | Firebase Authentication (Google) |
| Backend | ASP.NET on .NET 10, one modular app in Azure Container Apps |
| Storage | One Azure Storage account: Blob (raw data and memory files), Table (indexes), Queue (events) |
| Live updates | SignalR, running inside the app |
| AI | Claude through Microsoft Foundry, behind a task-based gateway |
| Infrastructure | Bicep with `.bicepparam` files, deployed by GitHub Actions |

## Status

The design is still being worked out. The decisions so far are in
[`docs/ideation/decision-log.md`](docs/ideation/decision-log.md), and the work is tracked in
[Issues](../../issues).
