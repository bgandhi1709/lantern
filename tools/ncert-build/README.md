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

Ollama runs on the Windows host, where it has the GPU and all the RAM, not WSL's share.

1. Install Ollama for Windows from ollama.com.
2. WSL2 in NAT mode can't reach Windows' `localhost`. So Ollama must listen on all interfaces: in
   Windows *Environment Variables*, add a user variable `OLLAMA_HOST` = `0.0.0.0`, then quit
   Ollama from the tray and start it again. Allow it through Windows Firewall when asked.
3. Pull the models in a Windows terminal: `ollama pull qwen3:8b` for drafts and
   `ollama pull qwen3-vl:8b-instruct` for figures. Each is about 5–6 GB and fits an RTX 3060 (12 GB)
   with room for an 8k context; Ollama swaps them, one at a time.

The tool finds Ollama through `OLLAMA_HOST` if set, otherwise `localhost`, then the WSL gateway (the
Windows host). Pick other models with `--model` / `LANTERN_DRAFT_MODEL` and `--vision-model` /
`LANTERN_VISION_MODEL`. A second Ollama inside WSL with no models (a snap install) is skipped.

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
  `LANTERN_GPU_RESUME_AT`). `scripts/night-run.sh --shutdown` runs everything class by class and
  shuts Windows down at the end; `--shutdown` also works on any single stage.
- **`title_guess` is a hint.** The draft model and the front matter give the real titles.
- **Younger classes have no numbered headings**, so their chapters are split one section per page.
