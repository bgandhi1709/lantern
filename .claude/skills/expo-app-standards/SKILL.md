---
name: expo-app-standards
description: Lantern's rules for the Expo / React Native / TypeScript app in client/. Use before adding or changing any code, screen, hook, API call or local storage under client/lantern-android, and when reviewing it. Covers structure, clean code, API boundaries and what may be stored on the device.
---

# Expo app standards

The Parent app (`client/lantern-android`) is written by an agent and reviewed by a founder who is new to Android and React Native. So the code must be boring, small and explained. When a rule below and a clever idea disagree, the rule wins.

Load the `expo` plugin skills that match the task (`expo-router` for routes, `expo-data-fetching` for API calls, `expo-dev-client` for native builds) after this one. Their advice never overrides a rule here.

## Steps

1. Read the story's Screens section and the canvas for the screen you build.
2. Find the nearest existing file of the same kind under `src/` and copy its shape. A new shape needs a reason in the PR.
3. Write the code under the rules below. Done when `npm run typecheck`, `npm run lint` and `npm run format:check` pass and every rule below is true of the diff.
4. Put a **Why this way** list in the PR: one or two plain lines for each new library, hook, native module or pattern, written for someone who has never seen it.

## Structure

- Routes live in `app/` (expo-router) and only render a screen. No logic, no fetch, no state in a route file.
- Everything else lives in `src/<feature>/` (`auth`, `family`, `catalog`) and shared plumbing in `src/shared/` (`api`, `storage`, `config`). A feature never imports another feature's internals; it imports its public `index.ts`.
- One component per file, under about 100 lines. When it grows, split out a hook (`useXxx`) for logic and keep the component for layout.
- Logic is a plain function that takes values and returns values, so it can be tested without React.
- No file named `utils`, `helpers` or `common`. Name the file for what it does.

## Clean code

- TypeScript strict. No `any`, no `!` non-null assertion, no `as` cast except in the one place a boundary parses data. Use `unknown` and narrow.
- A fixed set of values is a string-literal union (`type Board = 'cbse' | 'ssc'`) or an `as const` object, never loose strings and never the `enum` keyword.
- A single value is `const NAME = …` in SCREAMING_CASE, declared in the one file that uses it, with the unit in the name (`WAKING_AFTER_MS`) and a name that says what it is for (`MARK_SIZE`, not `SIZE`). Export it only when a second file needs it, and then import it; never copy it. A map from a fixed set to something else is a `Record<Union, T>`, so a new value without an entry fails the build.
- Failure is an exception or a typed error, not a `boolean` or a magic `null`. The API client throws `ApiError` with the problem code; one place turns codes into messages for the Parent.
- Short functions, one job each. Early return over nested `if`. Names say what, not how.
- No comments (`scripts/check-no-new-comments.sh` fails a commit that adds one). A comment means the code is too complicated: rename, extract or simplify. The reason for a decision goes in the ADR or decision log.
- No hard-coded app text. Every word the Parent sees lives in `src/shared/i18n/en.ts` and is read with `useStrings()`; a new language is a copy of that file typed as `Strings`, so a missing key fails the build. Accessibility labels count as text.
- No hard-coded environment value (URLs, keys, timeouts). Read it from `src/shared/config.ts`, which reads `EXPO_PUBLIC_*` and refuses to start when one is missing.
- Remove unused imports and dead code in every file you touch. No `console.log` in committed code.
- Reuse before writing: Expo SDK module first, then a small well-known package, then your own code. Install with `npx expo install` so versions match the SDK. Every library must support Android and iOS (ADR-0007, web dropped by D67), have no high or critical advisory (`npm audit --omit=dev` on a scratch install of the exact version, whose new packages must add none), and use a licence that fits PolyForm Noncommercial: MIT, ISC, BSD, Apache-2.0, BlueOak and 0BSD are fine, anything else (MPL, GPL, LGPL, AGPL, unknown) needs the founder's yes. Put the result in the PR's **Why this way**.
- Style with class names (NativeWind, ADR-0011), never a `StyleSheet.create` block or a style object. Use the composite classes and shared parts in **Styling structure** below before spelling classes out: a test fails on a combination a composite already names. Colours, fonts, radii and text sizes are tokens in `client/lantern-android/global.css`: use `bg-ground`, `text-muted`, `font-inter-bold`, `text-heading`, and add a token there (and in `theme.ts` when code needs the value) before inventing a one-off value. Only `Animated` values and SVG take a `style` prop, and so does a native control that has no `className`, such as `Picker.Item`, which takes one named style value built from the theme colours (D127). Keep line heights as ratios, use `pt-safe` and `pb-safe` instead of `SafeAreaView`. Lint enforces it.
- Reuse a component before writing one, and give every component a Storybook story with one export per state (`storybook-standards`, ADR-0012). A screen that reads a hook is a container over a view that takes its state as props.
- An async action on a screen ignores a second press while it runs, ignores Back while it runs, and drops its result when the screen has unmounted; it catches its own rejection and shows the Parent a message.
- A validation message appears only once the Parent's entry can no longer become valid: a second typing that is still a start of the first is not an error, and a live region that fires on every keystroke is noise on TalkBack.
- Accessibility is part of done: touch targets at least 48 px, a label on every icon-only control, real buttons, text contrast 4.5:1, state never shown by colour alone.

## Styling structure

A screen file holds layout and role names, never a look. The look lives in four layers, and a change goes in the lowest layer that owns it (ADR-0011 and its addendum, D128).

| Layer | Where | What it holds |
|---|---|---|
| Tokens | `global.css` `@theme` | Colours, fonts, radii, text sizes (`bg-ground`, `text-muted`, `font-inter-bold`, `text-heading`) |
| Composite classes | `global.css` `@utility` with `@apply` | A named combination of tokens, used in markup like any class. A composite may nest another (`lantern-card-padded` is `lantern-card p-4`) |
| Shared parts | `src/shared/ui/` | Structure that repeats: `ChoiceRow`, `ScreenHeading`, `TopBar`, `ErrorText`, `PrimaryButton`, `OutlineButton`, `TextField`, `Picker` |
| Screens | `src/<feature>/` | Layout (`flex-row`, `gap-3`, `mt-5`) plus composite and shared-part names |

**Composite classes** (list them with `grep '@utility' client/lantern-android/global.css`). Every name starts with `lantern-` (no clash with Tailwind, easy to grep) and says the job, not the look or the screen: `lantern-hint`, not `type-small-muted`; one style under several screen names would bring the repetition back. Only a look that exists on one screen is named after that screen.
- Text roles: `lantern-screen-title`, `lantern-item-title`, `lantern-paragraph`, `lantern-label`, `lantern-intro-text`, `lantern-error-message`, `lantern-link`, `lantern-hint`, `lantern-small-label`, `lantern-section-label`, `lantern-chapter-meta`, `lantern-field-error`, `lantern-footnote`. Pick the role by what the text is, not by its look.
- Frames: `lantern-screen` (page frame with safe areas), `lantern-card` (card edge), `lantern-card-padded` (the card with padding), `lantern-chip`, `lantern-icon-badge`, `lantern-button`, `lantern-input-box` (the box of a text field or dropdown), `lantern-screen-footer`.
- Selectable row: `lantern-pick-row` with `lantern-pick-row-selected` or `lantern-pick-row-unselected`.

**Rules:**
1. Never spell out a combination a composite names. `src/test/classStrings.test.ts` reads `global.css`, scans every `className`, every `*ClassName` prop and every template or ternary literal (nested ones too) in `src` and `app`, and fails when a string holds all the tokens of a composite of three or more. Fix it by using the composite, not by editing the test. The same test fails on a `lantern-` class that `global.css` does not define, so a typo or a leftover name cannot silently style nothing.
2. Add a composite when a combination is used a third time, in `global.css` with `@utility` and `@apply`, with a unit test case if it is a new kind of rule. A one-off combination stays spelled out in the screen. Never build a composite from a value that is not a token.
3. Reuse a shared part before writing markup: a heading with an intro is `ScreenHeading`, a back arrow with a title is `TopBar`, an error line is `ErrorText` (its live region is built in), a row the Parent selects is `ChoiceRow` (role `radio` for one of many, `lantern-button` for a switch), a Class or Birth year is `Picker`. A new shared part needs a story with one export per state (`storybook-standards`) and a reason it is not a class.
4. Pressed feedback is built into `lantern-button`, `lantern-pick-row` and `lantern-chip` (`active:opacity-70`). Anything tappable uses one of them; a new tappable look adds `active:opacity-70` itself.
5. A change to a text role, a card or a field is one edit in `global.css`; a change to what a shared part shows is one edit in that file. If a change needs edits in several screens, a layer is missing.
6. Board spacing is design, not drift: half steps such as `py-2.5` and `px-3.5`, heights 52, 56, 64 and 68, and the two link colours (`#0F766E` on About, `#115E59` on the Delete screens) come from the Design board. Do not snap or merge them. A look change on purpose needs a decision-log entry and goes in the PR as a deliberate change.
7. A label in a fixed-width slot (a step label, a tab) takes `numberOfLines={1}` with `adjustsFontSizeToFit` and a named `minimumFontScale`, so Gujarati, Hindi and large system text shrink instead of breaking mid-word. A body or heading wraps instead.
8. A native control that paints its own background (the dropdown) gets its border as an overlay above it (`absolute inset-0 pointer-events-none`), because the control can sit taller than its box and cover the border.
9. A screen that waits on the server shows a status, never a blank: `FamilyStatus` before a Child is known, the Subject list's own loading and failed rows after. A 429 is waited out, not shown as a failure.

## API boundary

- All calls go through one client in `src/shared/api`. It adds the Firebase ID token, sets a timeout, and never retries a write.
- Parse every response with a schema (zod) at that boundary and use the inferred type everywhere after. A response that does not parse is an `ApiError`, not a crash.
- The server is the source of truth for Family, Children, Subjects and Chapters. Fetch them; do not copy them into storage.
- Never put a token or personal data in a URL, a log line or an error message.

## Local storage

Everything on a phone can be read by whoever holds an unlocked phone, a backup, or a rooted device. Store as little as possible.

**What may be stored, and where:**

| Data | Where | Rule |
|---|---|---|
| Sign-in session (tokens) | The native Firebase SDK's own store (D72); the app never writes a token itself | Never in AsyncStorage, files or `localStorage` |
| Preferences that are not personal (welcome banner dismissed, active Child id, theme, text size, colour scheme) | AsyncStorage through the storage wrapper | The Child is stored as its opaque id, never its name; the look is three short words, cleared by `clearAll()` |
| Unlock wait (`unlockWait`, D112) | AsyncStorage through the storage wrapper | A count of wrong tries on the Unlock screen and the time the next try is allowed; no secret and nothing personal. Slows guessing by someone holding the phone, kept across restarts; cleared on a correct unlock and by `clearAll()` |
| Usage-number settings (D68, D118) | The native Firebase Analytics and Performance SDKs' own store | A random installation ID and whether collection is on. The app writes neither and sends no name, email, Child detail or user ID with them; the privacy notice lists them. Collection is switched off at every launch and by sign-out |
| Intro seen (`introSeen`, D75) | AsyncStorage through the storage wrapper | A boolean so later launches play the short intro; cleared by `clearAll()` |
| The Family key (made on the phone, wrapped by the Recovery code, D66, ADR-0015) | `expo-secure-store` (Android Keystore) | Founder-approved. It lets the phone lock and unlock personal details without asking for the Recovery code on every start. Kept per signed-in account as a map from uid to key, written only once registration succeeds (D87) and cleared by `clearAll()` (not by Sign out, D109); the Recovery code is never stored |
| Child names, birth year, School, Region, email, Subjects, Chapters, answers | Nowhere on the device | Fetched from the API each time and unlocked with the Family key; held in memory only |
| Secrets, API keys, the UAT address as a credential | Nowhere | The UAT address is baked in at build time (D61); there are no other secrets in the app |

**Rules:**

1. One module, `src/shared/storage`, is the only code that imports AsyncStorage or SecureStore. Lint forbids the import anywhere else.
2. Keys are typed and versioned, `lantern.v1.<name>`. A value has a schema; reading validates it and returns the default when the value is missing, corrupt or from an old version. Storage can throw or return nothing, so the app always works without it.
3. Values are small and plain (a string, a flag, an id). No object graphs, no blobs, no cache of API responses.
4. Family delete calls one `clearAll()` that wipes secure and plain storage together. It is tested. A Child delete keeps the Family key and the other settings, so it only moves the active Child to another one (D127). Sign out does not: it only ends the sign-in, and the phone keeps its Family key and settings until the app is deleted (D109).
5. Android backup is off (`android.allowBackup: false` in `app.json`), so no storage leaves the phone through cloud backup or `adb backup`.
6. No file writes to shared or external storage and no storage permission. If a file is ever needed, it goes in the app's own document directory.
7. A new thing stored on the device needs a line in the PR saying what, why, where and when it is cleared, and the founder's approval, because it changes what the Consent covers.

## Tests

- React Native Testing Library drives the screens with the API and the Firebase session faked.
- Every storage rule above has a test: missing value, corrupt value, old version, clear on sign out.
- Test behaviour a Parent sees (text, disabled buttons, navigation), not component internals.

## Enforced by tools

Where a rule can be a lint rule, make it one instead of trusting this page: `no-explicit-any`, `no-non-null-assertion`, `no-console`, `no-restricted-imports` for AsyncStorage and SecureStore outside `src/shared/storage`, for `@react-native-firebase/app` and `auth` outside `src/shared/session`, for `analytics` and `perf` outside `src/shared/analytics`, for the Google sign-in module outside `src/auth`, for the crypto library outside `src/shared/crypto`, import-boundary rules between features, and `no-restricted-syntax` for app text written in JSX or label props instead of `src/shared/i18n`. A rule a review caught twice becomes a check.
