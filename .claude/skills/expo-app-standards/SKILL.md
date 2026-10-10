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
- Failure is an exception or a typed error, not a `boolean` or a magic `null`. The API client throws `ApiError` with the problem code; one place turns codes into messages for the Parent.
- Short functions, one job each. Early return over nested `if`. Names say what, not how.
- Comments: only a rare one-line why (a platform quirk, a security property). No comment that restates the code.
- No hard-coded app text. Every word the Parent sees lives in `src/shared/i18n/en.ts` and is read with `useStrings()`; a new language is a copy of that file typed as `Strings`, so a missing key fails the build. Accessibility labels count as text.
- No hard-coded environment value (URLs, keys, timeouts). Read it from `src/shared/config.ts`, which reads `EXPO_PUBLIC_*` and refuses to start when one is missing.
- Remove unused imports and dead code in every file you touch. No `console.log` in committed code.
- Reuse before writing: Expo SDK module first, then a small well-known package, then your own code. Install with `npx expo install` so versions match the SDK. Every library must support Android and iOS (ADR-0007, web dropped by D67), have no high or critical advisory (`npm audit --omit=dev` on a scratch install of the exact version, whose new packages must add none), and use a licence that fits PolyForm Noncommercial: MIT, ISC, BSD, Apache-2.0, BlueOak and 0BSD are fine, anything else (MPL, GPL, LGPL, AGPL, unknown) needs the founder's yes. Put the result in the PR's **Why this way**.
- Style with class names (NativeWind, ADR-0011), never a `StyleSheet.create` block or a style object. Colours, fonts, radii and text sizes are tokens in `client/lantern-android/global.css`: use `bg-ground`, `text-muted`, `font-inter-bold`, `text-heading`, and add a token there (and in `theme.ts` when code needs the value) before inventing a one-off value. Only `Animated` values and SVG take a `style` prop. Keep line heights as ratios, use `pt-safe` and `pb-safe` instead of `SafeAreaView`. Lint enforces it.
- Reuse a component before writing one, and give every component a Storybook story with one export per state (`storybook-standards`, ADR-0012). A screen that reads a hook is a container over a view that takes its state as props.
- Accessibility is part of done: touch targets at least 48 px, a label on every icon-only control, real buttons, text contrast 4.5:1, state never shown by colour alone.

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
| Preferences that are not personal (welcome banner dismissed, active Child id) | AsyncStorage through the storage wrapper | The Child is stored as its opaque id, never its name |
| Intro seen (`introSeen`, D75) | AsyncStorage through the storage wrapper | A boolean so later launches play the short intro; cleared by `clearAll()` on sign out |
| The Family key (derived from the Passphrase, D66) | `expo-secure-store` (Android Keystore) | Founder-approved. It lets the phone lock and unlock personal details without asking for the Passphrase on every start. Cleared by `clearAll()`; the Passphrase itself is never stored |
| Child names, birth year, School, Region, email, Subjects, Chapters, answers | Nowhere on the device | Fetched from the API each time and unlocked with the Family key; held in memory only |
| Secrets, API keys, the UAT address as a credential | Nowhere | The UAT address is baked in at build time (D61); there are no other secrets in the app |

**Rules:**

1. One module, `src/shared/storage`, is the only code that imports AsyncStorage or SecureStore. Lint forbids the import anywhere else.
2. Keys are typed and versioned, `lantern.v1.<name>`. A value has a schema; reading validates it and returns the default when the value is missing, corrupt or from an old version. Storage can throw or return nothing, so the app always works without it.
3. Values are small and plain (a string, a flag, an id). No object graphs, no blobs, no cache of API responses.
4. Sign out, Family delete and Child delete call one `clearAll()` that wipes secure and plain storage together. It is tested.
5. Android backup is off (`android.allowBackup: false` in `app.json`), so no storage leaves the phone through cloud backup or `adb backup`.
6. No file writes to shared or external storage and no storage permission. If a file is ever needed, it goes in the app's own document directory.
7. A new thing stored on the device needs a line in the PR saying what, why, where and when it is cleared, and the founder's approval, because it changes what the Consent covers.

## Tests

- React Native Testing Library drives the screens with the API and the Firebase session faked.
- Every storage rule above has a test: missing value, corrupt value, old version, clear on sign out.
- Test behaviour a Parent sees (text, disabled buttons, navigation), not component internals.

## Enforced by tools

Where a rule can be a lint rule, make it one instead of trusting this page: `no-explicit-any`, `no-non-null-assertion`, `no-console`, `no-restricted-imports` for AsyncStorage and SecureStore outside `src/shared/storage`, for `@react-native-firebase/*` outside `src/shared/session`, and for the Google sign-in module outside `src/auth`, import-boundary rules between features, and `no-restricted-syntax` for app text written in JSX or label props instead of `src/shared/i18n`. A rule a review caught twice becomes a check.
