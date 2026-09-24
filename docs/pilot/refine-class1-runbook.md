# Refinement pilot: Class 1 (runbook)

Stage two of the NCERT layer (#34): Claude turns each chapter's local-model draft into concept cards
and parent-facing Q&A, in English (Gujarati is out of scope for now). This pilot measures how much of
the Claude plan one chapter costs before the other classes are scheduled.

**Scope:** Class 1, 22 chapters: *Joyful Mathematics* (`aejm1`, 13) and *Mridang* (`aemr1`, 9).
**Model:** Sonnet. Class 1 is simple, and Opus spends the plan's allowance much faster.
**Output:** `lantern-data/refined/<book>/<chapter>.json`, contract `refine-v1`, never in the repository.

## Why this is cheap

| Lever | How |
|---|---|
| One small input | Each chapter is one bundle file of about 1.5k tokens: draft hints and the cleaned text. No PDFs. |
| Fresh context per chapter | `/clear` between chapters, so earlier chapters never ride along. |
| No narration | The skill writes one file and replies with one line. |
| No self-review by reading | `ncert-build check` validates locally and for free. Claude only fixes what it reports. |
| No deep thinking | Sonnet, and nothing in the request asks it to think hard. |

## Before the session (WSL)

```bash
cd "/mnt/e/Personal Work/lantern/tools/ncert-build"
.venv/bin/ncert-build bundle --books aejm1 aemr1      # already done for Class 1
```

## Session A: measure three chapters

1. Start a **new** Claude Code session in the repository, on Sonnet:
   ```bash
   cd "/mnt/e/Personal Work/lantern" && claude --model sonnet
   ```
2. Run `/usage` and write down **session %** and **weekly %** (row "before" below).
3. For each of `aejm102`, `aejm103`, `aejm104`, type (`aejm101` was refined while the skill was being tested):
   ```
   /ncert-refine aejm102
   ```
   Then run `/clear` before the next one. The first time, allow the write to `lantern-data/refined/`
   and the `ncert-build check` command, choosing "don't ask again".
4. Run `/usage` again (row "after 3").
5. Check the output and read one chapter:
   ```bash
   tools/ncert-build/.venv/bin/ncert-build check aejm102 aejm103 aejm104
   ```

| Point | Session % | Weekly % | Clock time |
|---|---|---|---|
| before | | | |
| after 3 | | | |
| **per chapter** = (after − before) ÷ 3 | | | |

## Decide

- **Chapters per 5-hour session** ≈ 100 ÷ per-chapter session %.
- **Class 1 (22 chapters)** needs 22 × per-chapter % of a session.
- **Quality gate:** answers are grounded in the chapter, pitched at a 6-year-old, and in words a
  mother with basic English can use. If they aren't, fix the skill before spending more.

## Session B: the rest of Class 1

Same loop for `aejm105`–`aejm113` and `aemr101`–`aemr109`, one `/clear` between chapters, and one
`/usage` reading at the end. Then `ncert-build status` shows the `refined` column filling in.
