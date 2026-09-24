# ncert-build

Build-time tooling for Lantern's NCERT layer (D15, D16, #33). It prepares data on a workstation and
never serves mothers.

```
catalog ─► download ─► extract ─► segment ─► draft (local model) ─► [Claude Code refinement, #34]
```

| Stage | What it does | Output (under the data directory) |
| --- | --- | --- |
| `catalog` | Reads NCERT's book list out of `textbook.php` | `catalog.json` |
| `download` | Fetches chapter PDFs and each book's front matter (contents page) | `pdf/<book>/<chapter>.pdf` |
| `extract` | The PDF's own text layer, page by page. No OCR. Symbol-font maths (θ, Δ, ∠, −) is decoded | `text/<book>/<chapter>.json` |
| `segment` | Strips print slugs and running headers, splits into sections, exercises and summary | `chapters/<book>/<chapter>.json` |
| `draft` | A local model lists concepts and kid-style questions per chapter | `drafts/<book>/<chapter>.json` |

Each stage skips chapters it has already done (`--force` redoes them), so an interrupted run resumes.
A failing chapter is reported and the run carries on.

## Data stays out of this repository

NCERT text is copyrighted and this repository is public. Output goes to `LANTERN_DATA_DIR`, by
default `../lantern-data` next to the repository. The tool refuses to write inside the repository.

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
3. Pull the draft model in a Windows terminal: `ollama pull qwen3:8b`. It is about 5 GB and fits
   an RTX 3060 (12 GB) with room for an 8k context.

The tool finds Ollama through `OLLAMA_HOST` if set, otherwise `localhost`, then the WSL gateway (the
Windows host). Pick another model with `--model` or `LANTERN_DRAFT_MODEL`.

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
- **`title_guess` is a hint.** The draft model and the front matter give the real titles.
- **Younger classes have no numbered headings**, so their chapters are split one section per page.
