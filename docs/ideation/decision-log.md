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
