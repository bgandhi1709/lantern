# Driving and asserting on the Parent app without screenshots

Researched 2026-10-11. Scope: `client/lantern-android` (Expo `~57.0.27`, React Native `0.86.3`, Reanimated `4.5.1`, package `com.lantern.app`, scheme `lantern`), Android only, Linux host, emulator `Small_Phone` (system image `android-37.2`, Google APIs, 16 KB pages).

No research convention existed in the repo (`docs/` holds `adr`, `agents`, `architecture`, `ideation`, `pilot`), so this note lives in `docs/research/`.

Nothing here was run on the emulator, Metro or Docker (another session owns them). Everything below comes from official docs and upstream source. Items marked **[untested]** need a first run.

## Recommendation: Maestro, with a one-line adb fallback

**Use Maestro CLI 2.11.0 (Apache-2.0).** Why:

1. **It does not hit the idle-state failure.** `adb shell uiautomator dump` fails with `ERROR: could not get idle state.` because AOSP's `DumpCommand` first calls `uiAutomation.waitForIdle(1000, 10000)` and gives up if the UI keeps emitting events (a looping animation such as the app's `PageTurnLoader` does exactly that). Maestro's on-device driver sets `setWaitForIdleTimeout(0L)`, reads all window roots directly, and taps with `clickNoSync`, so none of those calls wait for idle.
2. **Output is small text.** `maestro hierarchy --compact` prints a CSV (one row per node, only non-empty attributes). `maestro test` prints one line per step.
3. **No app change, no native rebuild.** Maestro "doesn't live inside your app" and needs no test APK or dependency. It drives the already-installed debug APK through ADB. It installs its own small driver APKs on the emulator on first use.
4. **Assertions and input are built in.** `assertVisible` (with `enabled:`), `tapOn`, `inputText`, `assertNotVisible`, `extendedWaitUntil`.
5. **Supports the emulator we have.** 2.11.0 (2026-09-29) added Android 17 (API 37) support. The install page still lists API 29, 30, 31, 33, 34 as supported, so API 37.2 is **[untested]**; if the driver will not start, fall back to the adb helper below.

### Exact commands to try (emulator, Metro and the debug APK already running)

```bash
# 1. Install (needs Java 17+; this host has JDK 17 under ~/.local/jdk). Installs to ~/.maestro.
curl -fsSL "https://get.maestro.mobile.dev" | bash
export PATH="$HOME/.maestro/bin:$PATH"
maestro --version

# 2. Compact screen read (instead of a screenshot). First run installs the driver APKs on the emulator.
maestro hierarchy --compact
#   header: element_num,depth,attributes,parent_num ; attributes = non-empty values plus
#   clickable/enabled/focused/checked/selected when true. Warnings go to stderr, so use 2>/dev/null.

# 3. One flow, assertions on text and enabled state.
mkdir -p .maestro && cat > .maestro/about-you.yaml <<'YAML'
appId: com.lantern.app
---
- launchApp            # no clearState: keep the Metro-loaded debug bundle
- assertVisible: "About you"
- tapOn: "Continue"
- assertVisible:
    text: "Continue"
    enabled: false
- inputText: "Asha"
YAML
maestro test --no-ansi .maestro/about-you.yaml
```

Notes for the first run:

- `assertVisible` and `tapOn` retry for up to 7 s; use `extendedWaitUntil` for longer waits. `waitForAnimationToEnd` never fails (it returns at its timeout, default 15 s), so it cannot hang a flow on a looping animation.
- After a tap Maestro may run `waitForAppToSettle`: it re-reads the hierarchy up to 10 times, 200 ms apart (about 2 s), and stops early only if two reads match. It does not fail on a looping animation.
- Prefer `tapOn: { id: "..." }` over text. React Native 0.86 exposes `testID` as the Android `resource-id` (`ReactAccessibilityDelegate.kt`: `info.viewIdResourceName = testId`), which is what Maestro's `id:` selector and the `resource_id` CSV column read. `accessibilityLabel` appears as content-description (the `accessibility` column). The app has only a handful of `testID`/`accessibilityLabel` uses today, so add `testID` to each tappable on the screens being verified.
- `inputText` is unreliable for non-ASCII on Android (Maestro known-issues). Lantern's test data should stay ASCII.
- Dev-client launcher: package.json has no `expo-dev-client`, so the debug APK from `expo run:android` loads Metro directly and `launchApp` is enough. If a dev launcher is ever added, open the bundle with `openLink` using `lantern://expo-development-client/?url=<url-encoded manifest URL>` (the SDK 57 form; SDK 58 and later also accept `lantern://?__expo_url=...`), plus `__expo_disable_onboarding=1&__expo_disable_auto_launch=1` to skip launcher UI (Expo docs).
- Maestro uses fixed port 7001 on the emulator for its driver (source: `MaestroDriverService`, default `port` argument). Run one Maestro at a time.

### Fallback if Maestro will not run on API 37 (no install, text only)

Retry the stock dump, filtered to the lines an agent needs (text, id, enabled), and fall back to logcat when the dump is refused:

```bash
ui() { # prints "text | id | desc | enabled | clickable" for visible nodes; retries while animating
  for _ in 1 2 3; do
    out=$(adb exec-out uiautomator dump --compressed /dev/tty 2>/dev/null) && [[ $out == *"<hierarchy"* ]] && break
    sleep 0.7
  done
  grep -o '<node [^>]*>' <<<"$out" \
    | sed -E 's/.* text="([^"]*)".*resource-id="([^"]*)".*content-desc="([^"]*)".*clickable="([^"]*)".*enabled="([^"]*)".*/\1 | \2 | \3 | en=\5 clk=\4/' \
    | grep -v -E '^ \|  \|  \| ' | sort -u
}
```

This still fails during a continuous animation. Use it only where animations are not running, and keep screenshots for animated screens (`docs/agents/app-workflow.md` already says so). The regexp depends on the attribute order of the stock dump **[untested]**.

## Comparison

Token numbers are estimates, not measurements. Screenshot cost uses Anthropic's published rule: a patch is 28 x 28 px and an image costs `ceil(w/28) x ceil(h/28)` tokens, capped at 1568 on the standard tier and 4784 on the high-resolution tier (Claude 4.7 and later). So a 1080 x 2400 PNG is about 3,350 tokens before the cap, a 360 x 800 downscale is about 380. A text row from a compact hierarchy is roughly 15 to 30 tokens, so a typical 20 to 40 node screen is roughly 500 to 1,000 tokens **[estimate]**.

| Option | Tokens per check | Idle-state behaviour | Setup cost | Licence | Notes |
|---|---|---|---|---|---|
| Screenshot read back (today) | ~1,500 to 3,400 each; many per run | n/a (pixels) | none | n/a | Only option that shows layout fidelity |
| Screenshot, downscaled/cropped (`adb exec-out screencap -p` then resize) | ~400 for 360 x 800 | n/a | resize tool on host | n/a | Layout checks only; text may be illegible when shrunk |
| **Maestro CLI** (`hierarchy --compact`, `test`) | ~500 to 1,000 per hierarchy read **[estimate]**; ~10 per flow step on pass | No wait: idle timeout 0, no-sync clicks, bounded settle (about 2 s), 7 s assertion retry | One `curl` install, Java 17+, driver APKs auto-installed; no app change; API 37.2 **[untested]** | Apache-2.0; no published GitHub security advisories | Stable id selectors need `testID` |
| Maestro MCP (`maestro mcp`, tool `inspect_screen` returns compact JSON) | Similar to CSV | Same driver | Same, plus MCP registration | Apache-2.0 | Tool text tells the model to copy `txt` verbatim, not read a screenshot |
| Expo MCP local tools (`expo-mcp` 0.2.4) | Screenshot tool returns an image; `find_view` returns small JSON | **Same failure**: it shells out to `uiautomator dump --compressed` and `input tap` | `npx expo install expo-mcp --dev` (adds a dev dependency to the app), `EXPO_UNSTABLE_MCP_SERVER=1 npx expo start`, Expo login | MIT | Tools: screenshot, tap, find view by `testID`, `collect_app_logs`, `open_devtools`, `expo_router_sitemap`. No full text-hierarchy tool |
| Appium 3 + UiAutomator2 driver 8.x | Page source is verbose XML unless filtered | Configurable: set `waitForIdleTimeout` (default 10,000 ms) to 0 | Node server, drivers, client script or MCP; heaviest of the black-box tools | Apache-2.0; Appium advisories (2 high, 3 medium in 2026) are patched in current releases | Overkill for one agent |
| Detox 20.x | Small (test runner output) | Waits for app idle by design (network, timers, animations, RN threads); looping animations are a known hang risk | Highest: Gradle changes, `androidTest` APK, Jest, `DetoxTest.java`, cleartext config; Expo setup is "different" and not documented on the Detox page | MIT | Needs a native rebuild and project edits |
| `uiautomator dump --compressed` (adb) | Full XML, large unless filtered | **Fails** while any animation keeps the UI busy | none | AOSP, Apache-2.0 | Filter with the helper above |
| `adb logcat -d -s ReactNativeJS:V '*:S'` | Tiny, only what the app logs | No UI access, so no idle problem | none; needs app-side logging | AOSP, Apache-2.0 | Confirms state and navigation, not pixels |
| `adb shell dumpsys activity top` | Large | Not subject to the accessibility idle wait **[untested]** | none | AOSP | Whether Fabric text views expose their text here is unverified; do not rely on it |

## Detail by topic

### 1. Maestro

- **Linux.** The install page documents a single command for Linux and macOS (`curl -fsSL "https://get.maestro.mobile.dev" | bash`), Java 17 or higher with `JAVA_HOME` set. The GitHub README says the CLI covers macOS, Linux and Windows (WSL). Known-issues recommend Java 17 or 21. Maestro Studio is a separate AppImage and is not needed.
- **Licence.** Apache-2.0 (`LICENSE` in the repo, GitHub API `license.spdx_id`). No published security advisories on the repo (GitHub advisories API returned 0). Latest release `cli-2.11.0`, 2026-09-29. Dev-only, never ships in the app.
- **`maestro hierarchy`.** Source: `PrintHierarchyCommand.kt`. Prints the view hierarchy of the connected device as pretty JSON; `--compact` prints CSV with header `element_num,depth,attributes,parent_num`, attributes being the non-empty values plus `clickable`, `enabled`, `focused`, `checked`, `selected` when true. Warnings go to stderr. It reinstalls the driver by default (`--reinstall-driver`, negatable), so pass `--no-reinstall-driver` on repeat calls to save time **[untested]**. The docs site has no page describing the output, so confirm with `maestro hierarchy --help`.
- **Assertions.** `assertVisible: "text"` or object form with `text`, `id`, `enabled`, `checked`, `focused`, `selected`; fails if the element is missing or, with `enabled: true`, disabled. Retries for 7 s. `tapOn` takes text, `id` or a point; `inputText`, `assertNotVisible`, `extendedWaitUntil` and `takeScreenshot` also exist.
- **Animations.** See the recommendation. The on-device service (`MaestroDriverService.kt`) configures UiAutomator with `setWaitForIdleTimeout(0L)` and `setWaitForSelectorTimeout(0L)`. The hierarchy dump (`ViewHierarchy.kt`) reads `getWindowRoots` by reflection and falls back to `rootInActiveWindow`. Taps use `clickNoSync`. A `waitForIdle(500)` appears in one other code path, but its timeout is bounded. `waitForAnimationToEnd` compares screenshots on the device side, and returns without error at its timeout.
- **Debug APK.** Works with the existing debug APK. Docs: "Maestro assumes the app is already installed before the test starts" and needs no test dependencies or special test APK. The React Native page says no npm packages or JS changes are needed and lists Expo Go, development builds and EAS Workflows as supported. Expo's own E2E guide uses Maestro (with an APK build profile in EAS).
- **Headless.** Maestro talks to the running emulator over ADB. A headless emulator (`emulator -no-window`) has no special flag in the docs. `--no-ansi` is listed in neither page I fetched; run `maestro test --help` to confirm it exists before relying on it **[untested]**. Reports: `--format junit|html|html-detailed`, `--output`, `--test-output-dir` (default `~/.maestro/tests`), `--debug-output`.
- **Cost of its own output.** Flow runs print a line per step; failures write a screenshot and `maestro.log` under the test output directory. Agents should read the failing step line and `maestro hierarchy --compact`, not the saved screenshot.
- **Risks.** (a) API 37.2 is newer than the documented support list; (b) it installs two helper APKs and reserves port 7001 on the emulator; (c) `inputText` non-ASCII caveat; (d) `INSTALL_PARSE_FAILED_NO_CERTIFICATES` on locked-down devices, which does not apply to a stock emulator.

### 2. Expo agent tooling

- **What it is.** The Expo MCP server is remote (`https://mcp.expo.dev/mcp`, Streamable HTTP, OAuth). It needs an Expo account. This is the `mcp__plugin_expo_expo__*` plugin (`authenticate`, `complete_authentication` are what is loaded here). The plugin's `.mcp.json` points at that URL.
- **Local capabilities.** SDK 54 or later. Needs `npx expo install expo-mcp --dev`, being logged in to Expo CLI with the same account, and `EXPO_UNSTABLE_MCP_SERVER=1 npx expo start`; reconnect MCP each time the dev server restarts; one dev server at a time. Local tools: `automation_take_screenshot` (full screen or a view by `testID`), `automation_tap` (coordinates or `testID`), `automation_find_view` (position, size, visibility), `collect_app_logs` (native and JS logs for a short window), `open_devtools`, `expo_router_sitemap`.
- **What the Android automation really does** (source, `AutomationAndroid.ts`): `adb shell input tap`, `adb shell screencap`, and `adb exec-out uiautomator dump --compressed /dev/tty` for `testID` lookups. So it inherits the same idle-state failure and offers no text-hierarchy dump. Its screenshot tool returns an image, the thing we want to avoid. Its log collector is the useful part.
- **Plans and login.** The docs tie only `search_documentation` to a paid plan ("requires an EAS paid plan"). `build_run` needs a GitHub repository. Local capabilities need an Expo login but the page names no plan. Docs say iOS local capabilities need macOS simulators; they do not state Android emulator or Linux support, though the source has an Android implementation.
- **Licence.** `expo-mcp` is MIT, package version 0.2.4 (2026-09-23), public repo `expo/expo-mcp`, no published advisories. It would be a new dev dependency inside the app, which the app workflow's library vetting would apply to.
- **Verdict.** Not the cheapest path. Useful extras: `collect_app_logs`, and the remote docs search if the founder has a paid plan.

### 3. Other tools

- **Detox** (MIT, 20.51.4, no published advisories). Android needs `minSdkVersion` 18+, Gradle edits, ProGuard exemptions, a `DetoxTest.java` under `androidTest`, cleartext config for `localhost` and `10.0.2.2`, and an `assembleAndroidTest` APK. The Detox project-setup page says Expo setup "is different and is not covered" and points to Expo's E2E guide (which uses Maestro). Detox synchronises with network, timers, animations (including Reanimated), RN threads; that is the opposite of what we need while loop animations run. Rejected.
- **Appium + UiAutomator2** (Apache-2.0; Appium 3.8.0, driver 8.7.0). `waitForIdleTimeout` defaults to 10,000 ms and can be set lower; `ignoreUnimportantViews` compresses the tree; page source is XML. Works, but needs a Node server and client code. Appium has had high-severity advisories this year (`GHSA-4356-hm4h-ww9c` fixed in `@appium/base-driver` 10.8.0; `GHSA-jwgx-mp9m-jwcr` fixed in `@appium/storage-plugin` 1.1.6); current releases are patched. Reasonable only if Maestro fails.
- **`uiautomator dump --compressed`.** `--compressed` only prunes layout-only nodes. It does not remove the `waitForIdle` call in `DumpCommand.java`, so it does not avoid the failure.
- **React Native `testID` and accessibility.** React Native's own docs describe `testID` only as "used to locate this view in end-to-end tests" and do not state the Android mapping. The source does: `ReactAccessibilityDelegate.kt` sets `viewIdResourceName = testId` with the comment that black-box frameworks "do not have access to view tags". So `testID` shows up as `resource-id` in any accessibility-based dump (uiautomator, Maestro, Appium). Note the RN `View` docs warn that `testID` and `nativeID` disable layout-only view removal for that view (negligible for a handful of tappables).
- **`adb logcat`.** JS `console.*` output is written to logcat with tag `ReactNativeJS` (`JSLogging.cpp`: `__android_log_write(logPriority, "ReactNativeJS", ...)`). Android's logcat docs give the filter form `adb logcat ReactNativeJS:V *:S` (allowlist by tag, silence the rest), `-v` formats (`brief`, `tag`, `threadtime` default) and `-b` buffers. `-d` (dump and exit) is used in the docs' example. Tiny output, no idle problem, but only shows what the app logs.

### 4. Cheap hybrid (recommended working pattern)

1. **Drive and assert with Maestro** flows (or single `maestro hierarchy --compact` reads) for text, enabled state and navigation. Add `testID` to tappables on the screens under verification.
2. **App-side breadcrumbs for state** (dev builds only): one `console.log('[nav] about-you')` per screen mount and `console.log('[state] continueEnabled=false')` on key state changes. Read them with `adb logcat -d -v brief ReactNativeJS:V '*:S'` (filter with `grep '^\[\(nav\|state\)\]'` to stay small). Clear first with `adb logcat -c` so each scenario starts clean. The repo's app standards decide whether and where logging is allowed; check `expo-app-standards` before adding it, and keep personal data out of logs.
3. **Screenshots only for layout fidelity** (spacing, colour, clipping, animated screens): capture with `adb exec-out screencap -p`, downscale to about 360 px wide (about 380 tokens) or crop to the region, and read one image per state, not one per step.
4. **Reserve the raw `uiautomator dump`** for screens known to be static, using the filtered helper.

## Open questions to settle on the first run

- Does Maestro 2.11.0 start its driver on `android-37.2`? If not, try the nearest listed API image or the Appium fallback.
- Does `maestro hierarchy --compact` show the `testID` values as `resource-id` for Fabric views in this app?
- Is `--no-ansi` accepted by `maestro test`? Does `--no-reinstall-driver` speed up repeat `hierarchy` calls?
- Is the founder happy for a dev-only tool with its own driver APKs and port 7001 on the emulator? (Apache-2.0, so no licence sign-off is needed.)

## Sources

Maestro
- Install page (Linux and macOS command, Java 17+, supported API levels): https://docs.maestro.dev/getting-started/installing-maestro and https://docs.maestro.dev/getting-started/installing-maestro/windows
- Repo, README, Apache-2.0 licence: https://github.com/mobile-dev-inc/Maestro and https://raw.githubusercontent.com/mobile-dev-inc/Maestro/main/LICENSE
- Android platform page (no instrumentation, ADB, app must be installed): https://docs.maestro.dev/get-started/supported-platform/android
- React Native platform page (Expo Go, dev builds, testID to id, no JS changes): https://docs.maestro.dev/get-started/supported-platform/react-native
- `assertVisible`: https://docs.maestro.dev/reference/commands-available/assertvisible
- Wait commands and `waitForAnimationToEnd`: https://docs.maestro.dev/maestro-flows/flow-control-and-logic/wait-commands.md and https://docs.maestro.dev/reference/commands-available/waitforanimationtoend.md
- Reports and output flags: https://docs.maestro.dev/cli/test-suites-and-reports
- Known issues (Unicode `inputText`, Java 17 or 21): https://docs.maestro.dev/extra-materials/troubleshooting/known-issues.md
- Changelog (2.11.0 Android 17 / API 37): https://github.com/mobile-dev-inc/Maestro/blob/main/CHANGELOG.md
- `hierarchy` command source: https://github.com/mobile-dev-inc/Maestro/blob/main/maestro-cli/src/main/java/maestro/cli/command/PrintHierarchyCommand.kt
- Android driver source (idle timeout 0, port 7001): https://github.com/mobile-dev-inc/Maestro/blob/main/maestro-android/src/androidTest/java/dev/mobile/maestro/MaestroDriverService.kt
- Hierarchy dump source: https://github.com/mobile-dev-inc/Maestro/blob/main/maestro-android/src/androidTest/java/dev/mobile/maestro/ViewHierarchy.kt
- No-sync click: https://github.com/mobile-dev-inc/Maestro/blob/main/maestro-android/src/androidTest/java/androidx/test/uiautomator/UiDeviceExt.kt
- Bounded settle: https://github.com/mobile-dev-inc/Maestro/blob/main/maestro-client/src/main/java/maestro/utils/ScreenshotUtils.kt
- Maestro MCP (`maestro mcp`, `inspect_screen`): https://github.com/mobile-dev-inc/Maestro/tree/main/maestro-cli/src/main/java/maestro/cli/mcp

Expo
- Expo MCP docs (remote server, local capabilities, paid-plan note): https://docs.expo.dev/eas/ai/mcp/
- `expo-mcp` repo and package (MIT, 0.2.4): https://github.com/expo/expo-mcp and https://www.npmjs.com/package/expo-mcp
- Android automation source (`input tap`, `screencap`, `uiautomator dump --compressed`): https://github.com/expo/expo-mcp/blob/main/packages/expo-mcp/src/automation/AutomationAndroid.ts
- Expo E2E guide (Maestro on EAS Workflows): https://docs.expo.dev/build-reference/e2e-tests/
- Dev client launch URLs: https://docs.expo.dev/develop/development-builds/development-workflows/

Android and React Native
- AOSP `uiautomator dump` (idle wait and error text): https://github.com/aosp-mirror/platform_frameworks_base/blob/main/cmds/uiautomator/cmds/uiautomator/src/com/android/commands/uiautomator/DumpCommand.java
- React Native `testID` to `resource-id`: https://github.com/facebook/react-native/blob/main/packages/react-native/ReactAndroid/src/main/java/com/facebook/react/uimanager/ReactAccessibilityDelegate.kt
- React Native `ReactNativeJS` log tag: https://github.com/facebook/react-native/blob/main/packages/react-native/ReactAndroid/src/main/jni/react/jni/JSLogging.cpp
- React Native View props and accessibility docs: https://reactnative.dev/docs/view and https://reactnative.dev/docs/accessibility
- Logcat filters: https://developer.android.com/tools/logcat

Other tools
- Appium UiAutomator2 driver (settings, Apache-2.0): https://github.com/appium/appium-uiautomator2-driver
- Appium advisories: https://github.com/appium/appium/security/advisories
- Detox project setup (Android requirements, Expo not covered): https://wix.github.io/Detox/docs/introduction/project-setup/
- Detox synchronisation: https://wix.github.io/Detox/docs/articles/how-detox-works
- Detox licence (MIT): https://github.com/wix/Detox

Token cost
- Claude vision, resolution and token cost: https://platform.claude.com/docs/en/build-with-claude/vision
