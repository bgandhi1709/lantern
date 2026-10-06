# ncert-build

Build-time tooling for Lantern's NCERT layer (D15, D16, #33). It prepares data on a workstation and
never serves mothers.

```
catalog ─► download ─► extract ─► segment ─► draft, pictures (local models) ─► bundle ─► [Claude Code refinement, #34]
```

| Stage | What it does | Output (under the data directory) |
| --- | --- | --- |
| `catalog` | Reads NCERT's book list out of `textbook.php` | `catalog.json` |
| `download` | Fetches chapter PDFs and each book's front matter (contents page) | `pdf/<book>/<chapter>.pdf` |
| `extract` | The PDF's own text layer, page by page. No OCR. Symbol-font maths (θ, Δ, ∠, −) is decoded, and stacked fractions are rebuilt from the page geometry (`3/4`, `(a×c)/(b×d)`) | `text/<book>/<chapter>.json` |
| `segment` | Strips print slugs and running headers, splits into sections, exercises and summary | `chapters/<book>/<chapter>.json` |
| `draft` | A local model lists concepts and kid-style questions, then merges near-duplicates | `drafts/<book>/<chapter>.json` |
| `pictures` | Finds every figure on every page (OpenCV), and a local vision model reads each: its kind, labels, one line, and counts checked against the pixels | `pictures/<book>/<chapter>.json`, `pictures/<book>/<chapter>/pN-i.png` |
| `bundle` | One compact Markdown input per chapter for Claude refinement, picture descriptions inline | `bundles/<book>/<chapter>.md` |
| `check` | Validates a refined chapter against the refine-v1 contract | (prints one line per chapter) |
| summaries | A few plain lines per Chapter for the Parent, written in a Claude Code session from the refined Chapter and saved by `scripts/write_summaries.py` (D71) | `summaries/<book>/<chapter>.json` |
| `review` | The GPU's agreement per class and figure kind, and a sheet to mark readings right or wrong by hand | `review/report-*.txt`, `review/sheet-*.html`, `review/score.txt` |
| `upload` | Copies all of the above to the private `ncert` blob container | Azure Storage |

Each stage skips chapters it has already done (`--force` redoes them), so an interrupted run resumes.
A failing chapter is reported and the run carries on.

## Data stays out of this repository

NCERT text is copyrighted and this repository is public. Output goes to `LANTERN_DATA_DIR`, by
default `../lantern-data` next to the repository. The tool refuses to write inside the repository.

## Cloud copy

The data directory is the working copy; the `ncert` container in the Lantern storage account is
where the output is kept (the account is in `infra/`, deployed by #19). Sign in with `az login`,
then run `ncert-build upload --account <storage account>`. Your account needs Storage Blob Data
Contributor, which the deployment grants to `DATA_UPLOADER_OBJECT_ID`
(`az ad signed-in-user show --query id -o tsv` prints yours).

## Setup

```bash
cd tools/ncert-build
python3 -m venv .venv && .venv/bin/pip install -e ".[dev]"
.venv/bin/pytest
```

### Local model (for `draft`)

Ollama runs inside WSL as the native Linux install, which uses the RTX 3060 through WSL's CUDA
passthrough. The snap package can't reach CUDA and runs on the CPU only, so it is stopped and
disabled (`snap stop --disable ollama`).

1. Install with the official script (`curl -fsSL https://ollama.com/install.sh | sh`); it reports
   "Nvidia GPU detected" and adds a systemd service on `127.0.0.1:11434`.
2. Pull the models: `ollama pull qwen3:8b` for drafts and contents pages, and
   `ollama pull qwen3-vl:8b-instruct` for figures and image contents pages. Each is about 5–6 GB and
   fits the 12 GB card with room for an 8k context; Ollama swaps them, one at a time.
3. `ollama ps` should show `100% GPU` while a stage runs.

A Windows-host Ollama (listening on `0.0.0.0`) works too. The tool finds Ollama through `OLLAMA_HOST` if set, otherwise `localhost`, then the WSL gateway (the
Windows host). Pick other models with `--model` / `LANTERN_DRAFT_MODEL` and `--vision-model` /
`LANTERN_VISION_MODEL`. An Ollama with none of the models is skipped.

## Usage

```bash
ncert=.venv/bin/ncert-build
$ncert catalog                                 # once, or to pick up new books
$ncert list --grades 1-5                       # what the filters select
$ncert run --books aejm1 jemh1                 # everything for two books
$ncert download --grades 1-10                  # all core English-medium books
$ncert status --grades 1-10
```

The default selection is English-medium books in the core subjects (English, Mathematics, EVS /
The World Around Us, Science, Social Science). That is 44 books and 465 chapters for Classes 1–10.
Add `--all-subjects` for arts, PE and vocational books, and `--medium Gujarati` (or another
language) for regional editions.

## SSC Board (#93, D53–D57)

`--board ssc` builds the Maharashtra State Board's Balbharati Books (English medium, Std 1–5:
English Balbharati, Maths, EVS) into `../lantern-data-ssc`, the source for the private `ssc`
container. Only ebalbharati's own PDFs are used (ADR-0009).

```bash
$ncert --board ssc catalog        # board.json (the Board profile) and the 16 Books
$ncert --board ssc download       # one whole-Book PDF each
$ncert --board ssc split          # contents page → toc/<book>.json and per-Chapter PDFs
$ncert --board ssc run            # download, split, then the usual stages
```

Two things differ from NCERT. ebalbharati has no API: `catalog` replays the site's ASP.NET filter
postback (Text Books, Standard, English medium, 2026). And it serves one PDF per Book, so `split`
finds the contents page by its shape, has `qwen3:8b` list the Chapters (`qwen3-vl` when the page is
an image), and then checks everything in Python: every title must be on the contents page, the
printed-to-PDF page offset is voted from the page numbers themselves, and each Chapter's title must
be on the page it starts at. A Book that fails any check is reported and not split. English
Balbharati Chapters are Units with their lessons listed; Maths and EVS Chapters are lessons. Ids are
readable: `ssc3-maths`, `ssc3-maths-04`. After `split`, the remaining stages run unchanged.

### Refining SSC through the Batch API (#97, D69)

```bash
source ~/.lantern_api.env
.venv/bin/python scripts/refine_batch.py submit --chapters ssc3-maths-04 … --efforts medium --budget-usd 10
.venv/bin/python scripts/refine_batch.py collect <batch id> --out refined      # or "refined-gate/{model}-{effort}"
.venv/bin/python scripts/fix_flagged.py --board ssc --budget-usd 1             # per-field fixes for `check`
```

One request per Chapter to Claude Sonnet 5.5 at half price: the rules as the system prompt, the
Chapter (text, picture readings, an English Unit's lessons) without the local draft, and a JSON
schema; ids, model and versions are filled in by code. `submit` refuses a batch whose worst case
passes the budget; `collect` waits, writes each Chapter, runs `check` and prints the real cost from
`usage`. Nothing retries; a truncated or failed request writes nothing. Medium effort cost $0.024
per Chapter across the 207.

## Super context (#41, D17–D20)

`bundle` (v1) gives Claude no view of a book's earlier chapters, so `prerequisites` in refine-v1
output is almost always empty. `super_context.py` and `bundle_v2.py` add a second, additive stage
that builds a graded, code-checked "Super context" section — earlier concepts from the *same book
only* (D17), each with a quote from the *current* chapter's own text, verified by Python
(`quote_found`, exact match, whitespace-normalised only) before Claude ever sees it. The pick
itself — whether the quote genuinely supports linking that earlier concept — is *not* verified by
code (D19, a known, accepted gap); this is why "the text wins" still applies to super context the
same way it applies to the draft hints.

- `super_context.py`: `earlier_concepts`, `first_appearance_index`, `cap_to_budget` (size-capped
  to 25% of the current chapter's own tokens), `quote_found`, and `ask_which_earlier_concepts` —
  the closed 8B question ("which earlier concepts does this chapter's text use, quote the line").
- `bundle_v2.py`: splices a "Super context" section into `bundle.render()`'s v1 markdown, between
  "Draft concepts" and "Chapter text". Everything from "Chapter text" onward is byte-identical to
  v1 — bundle v2 is purely additive, per the hard constraint that `bundle.py` and `bundles/` stay
  untouched. Output path: `bundles-v2/<book>/<chapter>.md` (not yet wired into the CLI — call
  `bundle_v2.render()`/`.write()` directly, or see the worked example in issue #41).
- Status: library-level, tested (fixtures + one real end-to-end run against `bejm1` using the real
  local `qwen3:8b`). Not yet a CLI stage — no `ncert-build bundle-v2` command exists yet, and the
  A/B test plan (issue #41 step 5) hasn't been run at scale.

## Claude API (`ncert_build/claude.py`)

A minimal client for direct Anthropic API calls, separate from the Ollama-based local models
above and from Claude Code cloud sessions (`scripts/cloud_refine_*.sh`, which refine whole books
by running the `ncert-refine` skill as an agent). This is the low-level primitive for small,
targeted, metered API calls — currently used by `scripts/fix_flagged.py` to correct individual
`ncert-build check` violations. It's deliberately generic: nothing in `claude.py` is fix-specific,
so the same `Claude.complete()` call is meant to be reused for refining a future state board's
chapters, or for a runtime "answer this question with this context" call — and to be wrapped by
an admin-side HTTP layer later without changing this module.

**Guardrails placed on every call, and why:**

- **No retries.** `complete()` raises on error and never retries; callers (`fix_flagged.py`) catch
  the error, log it, and move to the next item. A flaky or wrong call costs one attempt, not a
  silent loop that burns budget.
- **A budget ceiling, checked before every call.** `fix_flagged.py --budget-usd` tracks real spend
  from the API's own `usage` field (not an estimate) and stops issuing new calls once the running
  total would exceed it, printing what's left unfixed rather than going over.
- **Minimal, single-field prompts.** Each call sends only the one offending field plus the rule it
  broke — never the whole chapter or the source bundle text. A chapter with one bad field costs
  one small call, not a full re-refine.
- **`max_tokens` kept small (120)** to match that minimal scope — deliberately tight enough that a
  runaway or off-task reply is cheap, not generous "just in case" headroom.
- **Sonnet 5.5's thinking is explicitly turned off** (`thinking: {"type": "between_tools"}`).
  Found the hard way: Sonnet 5.5 runs adaptive thinking by default, and thinking tokens count
  against `max_tokens` — a small budget can be consumed entirely by invisible reasoning, returning
  **empty visible text** while still billing for the call. Haiku 4.5 has no thinking to disable, so
  this is a no-op there.
- **Never write an empty reply over existing content.** `fix_flagged.py` checks the reply text
  before applying it; an empty or clearly-too-long result is reported and the original value is
  left in place, never blindly written. (This guard exists because the thinking bug above did once
  overwrite ten fields with empty strings before it was caught and reverted from the cloud-session
  git branches — see the fix-up pass in the project history.)
- **Cheapest adequate model for the task**: Haiku 4.5 by default for these mechanical
  shorten/rewrite edits, not Opus or Sonnet by default — the task doesn't need more reasoning than
  that, and every call's real `$` cost is printed so a heavier model is a deliberate choice, not a
  default.

**A known limitation this surfaced, not yet fixed by more API calls:** the `no markdown in
answers` check in `refine_check.py` flags any bare `_` character, including underscores used as
fill-in-the-blank placeholders in genuine exercise content (`"In b_s, a bus"`). Asking the model to
"remove markdown" on these just destroys the blank. This needs a rule change in `refine_check.py`
(e.g. only flag paired/markdown-shaped underscores), not a retry loop — left as a handful of known
false positives rather than spending more to satisfy a check that's wrong for this content.

## Known limits

- **Maths layout is flattened.** Superscripts, fractions and roots lose their position:
  `3√2` can come out as `32`. Pages dense with maths are flagged `maths_dense` in `text/` and
  listed in each chapter's `pages_needing_vision`, for an optional vision pass later.
- **Pictures are read by a small model.** Younger classes teach on pictures: tally sticks, rows
  of objects, fingers, number charts; older ones on unit-square grids and diagrams. `pictures`
  crops each figure and `qwen3-vl:8b-instruct` reads it in two steps: the kind of figure (17 NCERT
  types), then, where a number is the lesson, a count written out before it is given, with a
  counting instruction for that kind. A count stands when a pixel check agrees (ruled grid lines,
  separate drawings) or two of three reads agree; otherwise it is marked unsure in the bundle.
  Reads of one model can agree and still be wrong, so the true accuracy comes from the review
  sheet, marked by hand. Use the instruct build: plain `qwen3-vl:8b` reasons before every answer
  even with thinking off, about 8 times slower.
- **Long GPU runs pause when the card is hot**: above 84 °C until 76 °C (`LANTERN_GPU_PAUSE_AT`,
  `LANTERN_GPU_RESUME_AT`). They run any hour by default; set `LANTERN_RUN_WINDOW` (e.g.
  `20:00-08:00`) to free the PC for part of the day instead. `scripts/night-run.sh --shutdown` runs
  everything class by class and shuts Windows down at the end; `--shutdown` also works on any
  single stage.
- **`title_guess` is a hint.** The draft model and the front matter give the real titles.
- **Younger classes have no numbered headings**, so their chapters are split one section per page.
