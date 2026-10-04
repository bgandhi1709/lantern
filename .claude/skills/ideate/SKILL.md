---
name: ideate
description: "Turn a raw idea into a milestone, a diagram, a spec and small stories on the Lantern board."
disable-model-invocation: true
---

# Ideate

The front half of the cycle: idea in, board-ready stories out. Each story then runs `docs/agents/workflow.md` from step 1.

An **epic** is a GitHub milestone. There is no epic issue type.

`to-spec`, `to-tickets` and `grill-with-docs` are user-invoked, so the Skill tool cannot fire them. Where a step names one, read its `SKILL.md` under `.claude/skills/` and follow it in place.

Keep steps 1 to 6 in one unbroken context window.

## Steps

1. **Listen.** The user describes the idea in their own words. Hold questions until they say they are done, then restate the idea in three lines. Done when the user confirms the restatement.

2. **Size.** If the idea is too foggy or too large to hold in one session (greenfield, many unknown decisions), stop and switch to `/wayfinder`. Otherwise continue.

3. **Grill.** Run `grill-with-docs`, at most 15 questions. The divergence guard in `workflow.md` applies. Done when every open question has a decision, and new terms and hard-to-reverse calls are in `GLOSSARY.md` or an ADR.

4. **Milestone.** List the open milestones (`gh api repos/{owner}/{repo}/milestones --jq '.[] | {number,title,description}'`) and ask which one the idea belongs to. If none fits, propose a title and a one-line description, and create it once the user says yes (`gh api repos/{owner}/{repo}/milestones -f title=... -f description=...`). Done when you have a milestone number.

5. **Diagram.** Draft one Mermaid diagram in chat:
   - `flowchart` showing data flow when the idea moves data between existing parts (request, queue, worker, storage, UI);
   - `flowchart` with `subgraph` boundaries, as an architecture view, when the idea adds or changes components or boundaries.

   Name nodes with `GLOSSARY.md` terms. Iterate until the user approves it. Use FigJam (`figma:figma-generate-diagram`) only when the user asks for it.

6. **Spec.** Follow `to-spec`. Add a `## Diagram` section holding the approved Mermaid block. Create the issue with `--milestone "<title>"` and add it to the Lantern board: `gh project item-add 2 --owner bgandhi1709 --url <issue-url>`.

7. **Stories.** Follow `to-tickets` against the spec issue. Each story is a sub-issue of the spec, carries the same milestone, and goes on the Lantern board with Status `Todo`. Done when every approved story is on the board with its milestone and blocking edges.

8. **Record.** Append a dated entry to `docs/ideation/decision-log.md` for each decision the grill settled that no ADR holds. Following the no-docs-only-commits rule, it ships with the first story's PR. Then `/clear`.
