# Reference check for moving tooling to lantern-utilities (#181)

Rule: a file moves only if nothing in Lantern needs it. Nothing means no workflow in `.github/workflows`
(`android.yml`, `api.yml`, `expo-doctor.yml`), no `.husky` hook, no `docker-compose` or Dockerfile, no Bicep, no test
project, and no script that one of those calls. A file that fails the check stays.

Decisions (2026-10-31): the move is a clean-up, not privacy, so git history is not rewritten. The one-time setup scripts
move, except `bootstrap.sh`, which stays so the pipelines and `kv-param.sh` are left alone (decided 2026-10-31). The agent skill scripts count as Lantern code and stay.

Scope added 2026-10-31: Lantern's docs no longer describe how the content is made. The README, `infra/README.md`, the
Android and local-stack READMEs, ADR-0013 and ADR-0014, the code glossary, the architecture diagram, the workflow routing
table and the test and Bicep comments now say the content comes from `lantern-utilities` (ADR-0017). The decision log
keeps its old entries as history and gets D120.

Method: `grep -rF <name>` over the whole tree (without `node_modules`, `dist`, `bin`, `obj`), then read each hit to
tell a call from a mention in a comment or doc.

| File | Referenced by | Verdict |
|---|---|---|
| `tools/ncert-build/` (31 package, 9 script, 19 test files, README) | Only comments in `LayoutTests.cs`, `ContentLayoutTests.cs`, `main.bicep`, `bootstrap.sh`; docs. No workflow, hook, Docker file or test project runs it. | Move |
| `.claude/skills/ncert-refine/` | Calls `tools/ncert-build`; `docs/pilot/refine-class1-runbook.md`. Nothing in the run path. | Move (with the tool, same relative path) |
| `docs/pilot/refine-class1-runbook.md` | Runs the pipeline only. | Move |
| `infra/bootstrap.sh` | Comments in `api.yml`, `main.bicep`; the fix message in `preflight.sh`; docs. Not called, but it sources `kv-param.sh`, which CI needs. | Stay (decided: keep it beside `kv-param.sh` and the Bicep params, no second copy) |
| `infra/github-setup.sh` | `infra/README.md` only. | Move |
| `client/lantern-android/ci/setup-signing.sh` | `client/lantern-android/README.md` only. `android.yml` decodes the key itself. | Move |
| `deploy/local/mkcert.sh` | **Run by `deploy/local/setup.sh`**, which `api.yml` runs for the Docker E2E. | **Stay** (issue expected a move) |
| `deploy/local/load-uat-masters.sh` | **Run by `scripts/verify-env.sh up`**, the working cycle. | **Stay** (issue expected a move) |
| `deploy/local/token.sh` | No script or workflow. Documented in `deploy/local/README.md` and the `verify` skill as the way to call the local stack. | Stay (part of the local stack's documented use) |
| `deploy/local/setup.sh`, `setup.ps1` | `api.yml`; `setup.sh` runs `setup.ps1`. | Stay |
| `infra/preflight.sh`, `keep-running-image.sh` | `api.yml` (`infra-check` and release). | Stay |
| `infra/kv-param.sh` | Sourced by `preflight.sh` (CI) and `bootstrap.sh`. | Stay |
| `scripts/pre-commit.sh` | `.husky/pre-commit`. | Stay |
| `scripts/verify-env.sh` | The working cycle; docs; `load-uat-masters.sh` caller. | Stay |
| `.claude/skills/*/scripts/*` (6 files) | Skill tooling. | Stay (decided) |
| `infra/cloudamqp-wizard.sh` | Git-ignored, not tracked. | Out of scope |

## What changes in Lantern after the move

- Delete the files marked Move.
- Fix references: `README.md` (line 30), `infra/README.md` (lines 19, 50-51, 73-76), `client/lantern-android/README.md`
  (line 92), `deploy/local/README.md` (line 62 mentions only the seeder), comments in
  `main.bicep`, `bootstrap.sh`, `LayoutTests.cs` and `ContentLayoutTests.cs` (the `ncert-build` mentions),
  `docs/agents/*` (checked: they mention only `verify-env.sh`, which stays).
- `.gitignore` keeps its NCERT entries, so a stray PDF still cannot be committed.
- Add the Catalog contract next to the Class manifest contract, then an ADR for the split.

## Notes for the move

- `ncert-build` writes its data to a sibling of the repo (`../lantern-data`, found three levels above `config.py`).
  Keep the path `tools/ncert-build/` in `lantern-utilities` and the default data folder stays where it is.
- The `ncert-refine` skill is found by Claude only when run from the repo that holds it, so refinement is run from
  `lantern-utilities` from now on.
