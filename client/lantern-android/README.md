# lantern-android

The Parent app: Expo, React Native and TypeScript (strict), with expo-router. It is one codebase, so it uses only libraries that support Android and iOS ([ADR-0007](../../docs/adr/0007-parent-app-in-expo.md), web dropped by D67); the POC targets Android only. Right now it is a scaffold: a placeholder screen with the Lantern wordmark and one line saying whether the API answered (`GET /health/live`), which is the first proof a phone can reach UAT. It is built and signed by GitHub Actions, so it can be installed on a real phone.

## Prerequisites

- Node.js 22 and npm.
- Android Studio with an emulator image that has Google Play services (the sign-in story needs it). Create a virtual device from a "Google Play" system image and start it before running the app.

## Install

```sh
cd client/lantern-android
npm install
cp .env.example .env
```

Set `EXPO_PUBLIC_API_BASE_URL` in `.env` to the API's base URL, for example the UAT URL. The app refuses to start without it. `.env` is git-ignored; no value is kept in code.

The emulator reaches your computer at `10.0.2.2`, not `localhost`. The local Docker API ([`deploy/local`](../../deploy/local/README.md)) is served over HTTPS with a local certificate, which the emulator does not trust, so point at UAT until the story that calls the API deals with it.

## Run on the emulator

With the emulator running:

```sh
npm run android
```

This starts the Metro bundler, installs Expo Go on the emulator if it is missing, and opens the app. The placeholder screen shows "Lantern". After editing `.env`, restart with `npx expo start --clear`.

## Build an APK for a real phone

The `Android - Check and Build` workflow ([`android.yml`](../../.github/workflows/android.yml)) checks every pull request that touches `client/**` (typecheck, lint, format, `expo-doctor`, and a bundle export). A manual run also builds a signed release APK.

**One-time setup** (the wizard writes everything to the `uat` environment of the repository; it needs Docker, `az` and `gh` signed in):

```sh
client/lantern-android/ci/setup-signing.sh
```

It creates one release key in `~/lantern-secrets/` (outside the repo), reads UAT's address from the deployed Container App, sets the five `uat` secrets, and prints the key's SHA-1 for the Firebase registration in the sign-in story. **Back up `~/lantern-secrets/`.** A lost key means a new SHA-1 and a new Firebase registration.

**Each build:**

1. GitHub → Actions → **Android - Check and Build** → Run workflow (any branch).
2. Approve the `uat` deployment when GitHub asks.
3. The build is lean: arm64-v8a only (so the APK installs on nearly every phone since about 2017 but not on an x86 emulator or an old 32-bit phone; use `npm run android` for the emulator), Gradle's downloads and unchanged tasks are cached between runs, and Gradle gets 4 GB of heap. The first run on a branch is slower than the ones after it.
4. When it finishes, download the `lantern-android-<n>` artifact from the run page (a zip with `app-release.apk`).
5. Put the APK on the phone (USB or any file transfer), open it, and allow "Install unknown apps" for the app you opened it from. Or with USB debugging on: `adb install app-release.apk`.
6. Open Lantern. The screen should say "Lantern is reachable". The first call after UAT has been idle takes up to about 30 seconds, and the screen says it is waking Lantern up meanwhile.

**What this exposes.** The repository is public and any signed-in GitHub user can download its artifacts, so:

- The UAT address is a secret on the `uat` environment, never in the repo, and masked in the logs. It is baked into the APK, so anyone who downloads the APK can read it. It is not a credential: the API needs a Firebase sign-in on every call.
- The artifact is deleted after one day.
- The signing key never leaves the secrets and your backup.
- A release build refuses an `http://` address, and the release manifest has no cleartext traffic (the workflow checks it).

## Checks

Run these from `client/lantern-android`. CI (`android.yml`) runs the same four on every pull request that touches `client/**`.

| Command                | What it does                                                                                              |
| ---------------------- | --------------------------------------------------------------------------------------------------------- |
| `npm run typecheck`    | `tsc` in strict mode                                                                                      |
| `npm run lint`         | ESLint: `eslint-config-expo` plus the rules below                                                         |
| `npm run format:check` | Prettier check (`npm run format` fixes)                                                                   |
| `npm test`             | Jest (`jest-expo`) with React Native Testing Library; one file while iterating: `npx jest src/shared/api` |

Lint fails on `any`, a `!` assertion, `console.*`, and an AsyncStorage or SecureStore import outside `src/shared/storage`. A feature folder (`src/auth`, `src/family`, `src/catalog`) may import another feature only through its `index.ts`. `src/test/lint.test.ts` proves each rule.

## Shared code

- `src/shared/storage`: the only code that touches the device store. Keys are `lantern.v1.<name>`, each with a schema and a default that a missing, corrupt or old-version value falls back to. `clearAll()` wipes the plain and secure stores. Android backup is off (`android.allowBackup` is `false`, and the release build checks the manifest).
- `src/shared/api`: the one API client. It times out, throws `ApiError` with the problem `code`, never retries, and parses every response with a zod schema. A hook supplies the Firebase ID token once the sign-in story wires it.
- `src/shared/config.ts`: the only reader of `EXPO_PUBLIC_*` and the API timeout.
- Tests fake the API (`src/test/fakeApi.ts`) and the device stores (`jest.setup.ts`, `src/test/fakeSecureStore.ts`).

## Notes

- `.npmrc` sets `legacy-peer-deps`: Expo SDK 57's optional peers (`react-dom`, `react-native-worklets`) conflict under npm's strict resolver.
- ESLint is pinned to 9: `eslint-plugin-react`, used by `eslint-config-expo`, does not run on ESLint 10 yet.
