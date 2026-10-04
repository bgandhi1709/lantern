## Agent skills

### Issue tracker

GitHub Issues via the `gh` CLI (`bgandhi1709/lantern`). See `docs/agents/issue-tracker.md`.

### Triage labels

Default five-label vocabulary (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Ideation

New ideas run `/ideate`: listen, grill, pick a milestone (epic = milestone), Mermaid diagram, spec, stories on the Lantern board. See `.claude/skills/ideate/SKILL.md`.

### Working cycle

Grill, plan, implement, verify, PR, doc sync, review, retro, with a tech-to-skill routing table and token rules. If the user diverges from a recorded architecture or decision, stop and grill them before proceeding (divergence guard). See `docs/agents/workflow.md`.

### Coding standards

Read during review. See `CODING_STANDARDS.md`.

### Domain docs

Single-context: one `GLOSSARY.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.
