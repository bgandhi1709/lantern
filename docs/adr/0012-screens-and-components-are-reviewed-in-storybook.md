# Every shared component and screen state is a Storybook story, reviewed on the emulator

The Parent app uses Storybook for React Native (`@storybook/react-native` 10.6.0) so that every reusable component and every state of a screen can be opened on the emulator or a phone from fixed data, with no Docker stack and no particular API state. A story is a small file beside the component (`PrimaryButton.stories.tsx`) with one named export per state.

**Why:** the founder is new to Android UI and wants the founder and the agent to look at the same screens, and Lantern will grow many screens that must reuse the same components. A story per state makes a missing state visible (disabled, busy, failed, a long Gujarati label) and gives the agent a list of what already exists before it builds a duplicate.

**Decided with the founder (issue #139):**

- **Where:** `.rnstorybook/` holds the config (`main.ts`, `preview.tsx`, `index.tsx` and the generated `storybook.requires.ts`). Stories sit beside their component under `src/`. `npm run start:storybook` starts Metro with `EXPO_PUBLIC_STORYBOOK_ENABLED=true`, and `app/index.tsx` then renders Storybook instead of the app.
- **Container and view:** a screen that reads a hook is split into a container and a presentational view that takes its state as props (`StartScreen` and `StartView`). The stories are for the view.
- **Release builds have none of it:** `metro.config.js` passes `enabled: false` to `withStorybook` unless the switch is on, and Metro then swaps every Storybook module for an empty one. The release bundle fell from 8.7 MB to 5.2 MB and holds no story, no `@gorhom/bottom-sheet` and no Storybook code. `config.ts` also refuses to start a release build with the switch on.
- **Addons:** Controls (change props live), Actions, Backgrounds (the app's `ground`, `card` and `primary` colours). Notes and the web addons were left out; the web addons do not run on the device.
- **Packages:** Storybook, its addons, `@gorhom/bottom-sheet` and `react-dom` are `devDependencies`. `react-dom` is there only because `@storybook/react` lists it as a peer and `expo-doctor` fails without it; the app never imports it (ADR-0007 still holds: no web). Three native modules it needs stay in `dependencies` so autolinking builds them: `react-native-gesture-handler` (SDK-pinned 2.32), `@react-native-community/slider` and `@react-native-community/datetimepicker`. They add native code to the APK; nothing uses them outside Storybook. All were vetted: no advisory beyond the ones already on main (`braces` through Metro, #141), and every licence is MIT-like (the one CC-BY-4.0 entry is `caniuse-lite` data, already there).
- **safe-area-context:** Storybook asks for 5.8.0 and Expo SDK 57 pins `~5.7.0`. The app keeps Expo's version (`.npmrc` has `legacy-peer-deps`).
- **No telemetry, no storage:** `metro.config.js` sets `STORYBOOK_DISABLE_TELEMETRY`, and Storybook is given no storage, so it keeps nothing on the device.
- **Tests:** `src/test/stories.test.tsx` renders every story, checks that the named states exist, and fails when a component file has neither a story nor a written reason in its list.
- **AI access (experimental):** `experimental_mcp: true` serves `localhost:7007/mcp` while Metro runs, listing components, stories and props, so the agent checks what exists before writing a component. It is development only and marked experimental by Storybook. The web `addon-mcp` is not used: it supports React web only.

Considered and rejected: a hand-made gallery route (no library, but our own code to keep and no controls), and Chromatic hosting (a paid outside service that would receive our screens).
