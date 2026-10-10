---
name: storybook-standards
description: Lantern's rules for Storybook stories in the Expo app in client/lantern-android. Use when adding or changing a component or screen, before building a new component (check what already exists), and when reviewing UI work. Covers what needs a story, how a story is written, and how to reuse before you build.
---

# Storybook standards

Storybook (ADR-0012) is where the founder and the agent look at the same components and screen states, on the emulator or a phone, from fixed data. Load this after `expo-app-standards` for any UI work. When a rule here and a clever idea disagree, the rule wins.

## Reuse before you build

1. Before writing a component, list what exists. With Metro running via `npm run start:storybook`, ask the Storybook MCP at `http://localhost:7007/mcp` (`docs-list`, then `docs-show` for the props); without it, read the `*.stories.tsx` files. This is experimental, so the story files are the fallback, not an extra step.
2. Use an existing component as it is. If it almost fits, add a prop and a story for the new state. A second component that does the same job needs a reason in the PR.

## What needs a story

- Every component file under `src/` has a `Name.stories.tsx` beside it, with one named export per state the Parent can reach: default, pressed or busy, disabled, error, empty, and a long label (Gujarati and Hindi run longer than English).
- A component that has none says why in the `withoutStory` list in `src/test/stories.test.tsx` (a container, a provider, an animation part shown inside another screen). The test fails when a file has neither, and when the list names a file that now has a story.
- A screen that reads a hook is split in two: the container calls the hook, and a view takes everything it shows as props (`StartScreen` and `StartView`). Stories are for the view. Never fake a hook in a story.

## How a story is written

- Component Story Format with TypeScript: a `meta` object `satisfies Meta<typeof Component>`, then `export const Name: StoryObj<typeof meta>`. Names are UpperCamelCase and say the state (`SigningIn`, not `Story2`). Titles are static strings, `Feature/Component`.
- Drive stories with `args`; put the shared ones on the meta. A callback is `fn()` from `storybook/test`, so Actions shows the press.
- Styling follows ADR-0011: class names only, no `StyleSheet` and no style objects (lint applies to stories too). A story holds no colours of its own; the Backgrounds addon already offers `ground`, `card` and `primary`.
- Text in a story comes from `src/shared/i18n` through the component. A long-label story is the one place a literal string is allowed, because it tests the layout.
- Fixed data only: no API, no Firebase, no storage in a story. Anything it needs is an arg.

## Review

- A PR that adds or changes a component lists the stories and shows an emulator screenshot of each state. The founder reviews states, not only the happy path.
- Stories check layout and state. They do not replace the Jest behaviour tests, and they do not check accessibility: labels, 48 px targets and contrast are still checked by hand.
- Storybook must stay out of release builds. After a change to `metro.config.js`, `app/index.tsx` or `.rnstorybook/`, export the bundle without `EXPO_PUBLIC_STORYBOOK_ENABLED` and check that it holds no Storybook code.

## Dependencies

Keep every `@storybook/*` package and `storybook` on one version, pinned exactly. A new addon is a new library: vet it first (workflow, Dependencies).
