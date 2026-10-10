# App working cycle

The cycle for anything under `client/` (the Expo / React Native / TypeScript Parent app). It replaces `docs/agents/workflow.md` for app work. Everything in that file not repeated here still holds: the divergence guard, token management, and the API-call guardrails. Work inline: no subagents unless the user asks for one by name.

The founder is new to Android and React Native, so the cycle front-loads rules and back-loads plain-language explanation. Review on GitHub is the last check, not the first.

## One story, one working slice

A story with a screen or a flow works end to end when it is done: real data, real calls, no inert buttons. The API and Azure changes it needs are sub-issues of the story and ship in the same PR train. A screen is a sub-issue of its story. Do not start a screen whose API sub-issue is not in the plan.

## Steps

| # | Step | How | Output |
|---|---|---|---|
| 0 | Ticket | Pick the screen sub-issue. Read its **Screens** section and the canvas board it names (read the canvas with the Artifact tool; it is private). The ticket's acceptance criteria are the spec; if the canvas and the ticket disagree, stop and ask. | Issue and board read |
| 1 | Grill | Only if the ticket leaves a behaviour open. `/grill-with-docs`, at most 5 questions. New terms to `GLOSSARY.md`, hard-to-reverse calls to an ADR. | Decisions |
| 2 | Tech pass | Load `expo-app-standards`, then only the matching `expo` plugin skills: `expo-router` (routes), `expo-data-fetching` (API calls), `expo-dev-client` (native modules, Firebase). Load `ponytail` for any new dependency. For the API sub-issue follow `docs/agents/workflow.md` and the C# skills it routes to. | Skills loaded |
| 3 | Plan | In chat: the files to add or change under `app/` and `src/`, which existing file each copies its shape from, every new library with its reason, what is stored on the device (usually nothing), the API calls, and the behaviours to test. A new library, a new stored value or a new native permission needs the founder's yes before step 4. | Plan approved |
| 4 | Implement | `/tdd`, one red-green slice at a time, with React Native Testing Library and the API and Firebase session faked. Keep each commit to one slice. Do the API/Azure sub-issue first when the screen depends on it. | Commits on the branch |
| 5 | Verify | Run in `client/lantern-android`: `npm run typecheck`, `npm run lint`, `npm run format:check`, `npx expo-doctor`, `npx expo export --platform android` and the tests. Then write the **Try it** steps the founder follows on the emulator (`npm run android`) or the signed APK (the `android.yml` manual run): each acceptance criterion as a tap-by-tap line with the expected result. Never claim a screen works on a device you did not run it on; say which checks ran and which are for the founder. | Check output and Try it list |
| 6 | Security | `/security-review` on the diff, then walk the Local storage table in `expo-app-standards` against it: what is stored, where, cleared when. Fix findings on the branch. | Findings fixed or recorded |
| 7 | PR | `/pr` with the acceptance criteria, the **Try it** steps, and a **Why this way** list: one or two plain lines for each new library, hook, native module or pattern, written for someone who has never seen it. Say what is stored on the device, or "nothing". | PR |
| 8 | Doc sync | `client/lantern-android/README.md` (run steps, scripts), root `README.md` (Status), `docs/ideation/decision-log.md`, ADRs, the canvas if the built screen differs from it. | Docs match the code |
| 9 | Review | The founder reviews on GitHub and on a device. Fix, reply as Claude, resolve the threads. | Resolved threads |
| 10 | Retro | `/retro` in the same session. A rule a review caught becomes a lint rule or a test, not a sentence. Add it to the ESLint config first, `expo-app-standards` second. | Environment fixes |

## Rules of this cycle

- **Standards:** `.claude/skills/expo-app-standards/SKILL.md` is the rule book. Review applies every rule in it.
- **Local storage:** the strictest area. Nothing personal on the device, one storage module, everything wiped on sign out, Android backup off. A new stored value is a founder decision, not an implementation detail.
- **Dependencies:** each new package must support Android and iOS (ADR-0007, web dropped by D67), be installed with `npx expo install`, be vetted before the founder's yes (a scratch install of the exact version: `npm audit --omit=dev` shows no high or critical advisory from it, and its licence and its dependencies' are MIT-like and fit PolyForm Noncommercial; MPL, GPL, LGPL, AGPL or an unknown licence needs the founder's yes), and be justified in **Why this way**. Then re-run the audit on the real install, since peer and runtime dependencies can differ from the scratch one.
- **Fidelity:** the built screen matches the canvas board in layout, copy and accessibility rules. A difference is either fixed or written into the ticket before the PR.
- **Shipping:** an app PR leaves `android.yml` green. The release APK is built by the manual run behind the `uat` approval; the UAT address stays a secret (D61).
- **Tokens:** read the specific files you need, run single test files while iterating and the full set once at the end, and keep chat replies short.
