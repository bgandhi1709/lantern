# The parent app is styled with NativeWind class names, with the look defined once in `global.css`

Screens in `client/lantern-android` style themselves with Tailwind class names through NativeWind v5 (`className="flex-1 bg-ground p-6"`), not with a `StyleSheet.create` block or a style object in the component file. The whole look is one file, `global.css`: its `@theme` block defines every colour, font, radius and text size as a token, and a class such as `bg-ground`, `text-muted`, `font-inter-bold` or `text-heading` reads it. A screen file then holds layout and class names only.

**Why:** the same body-text style had been written out five times, and the founder, who is new to Android UI, asked for ready-made classes that are assigned in the markup. One token file also gives the agent and the founder one place to read and change the look, and it is what the planned screen gallery or Storybook (#139) will show.

**Decided with the founder (issue #138):**

- **Version:** NativeWind 5.0.0-rc.0 with `react-native-css` 3.1.0-rc.0, Tailwind 4.1.12, `@tailwindcss/postcss` 4.1.12 and `lightningcss` 1.30.1, pinned exactly. v4 was rejected: its Tailwind 3 dependency chain carries five high advisories (`tailwindcss`, `braces`, `micromatch`, `fast-glob`, `chokidar`). v5 is a release candidate paired by its authors with Expo SDK 57 and React Native 0.86; if a later release breaks the build, pin back or move to the next candidate.
- **Reanimated and Worklets:** `react-native-css` imports `react-native-reanimated` at runtime, so `react-native-reanimated` 4.5.1 and `react-native-worklets` 0.10.1 (the versions Expo SDK 57 bundles) are dependencies too. They add native code, so the app needs a native rebuild when they change.
- **Licence:** `lightningcss` and its platform binaries are MPL-2.0. The founder accepted it as an unmodified build-time tool. It was already in the lockfile through Expo's Metro, and a check confirmed that neither the APK nor the JS bundle contains it.
- **Scope:** every screen and shared component uses class names. The only style objects left are `Animated` values and SVG, which cannot be class names; lint enforces this (`no-restricted-syntax` bans `StyleSheet.create` and `style={{…}}` elsewhere).
- **Tokens:** `global.css` is the source. `src/shared/ui/theme.ts` keeps only what code needs as values (the SVG colours and the easing curve), and `theme.test.ts` fails if a colour differs from `global.css`.

**Rules that follow from how the library behaves** (found while building it):

- One spacing step is 4 px (`--spacing: 4px`), so `p-6` is 24 px whatever the rem size. Every token is in px.
- A line height is a ratio (`calc(36 / 28)`), because `react-native-css` reads a unitless `line-height` as a multiple of the font size; a `36px` value gave a line height of about a thousand pixels.
- `SafeAreaView` from `react-native-safe-area-context` does not take class names. Use a `View` with `pt-safe` and `pb-safe`.
- A component that animates keeps an `Animated.View` with a `style` object and puts its class names on a plain `View` or `Text` inside or around it.

Considered and rejected: `StyleSheet.create` blocks with shared text styles in `theme.ts` (no new library, but not the classes the founder asked for), and a styles file per component (separates the code but does not remove the duplication).
