# lantern-android

The Parent app: Expo, React Native and TypeScript (strict), with expo-router. It is one codebase, so it uses only libraries that support Android, iOS and web ([ADR-0007](../../docs/adr/0007-parent-app-in-expo.md)); the POC targets Android only. Right now it is a scaffold: one placeholder screen with the Lantern wordmark.

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

## Checks

| Command | What it does |
| --- | --- |
| `npm run typecheck` | `tsc` in strict mode |
| `npm run lint` | ESLint with `eslint-config-expo` |
| `npm run format:check` | Prettier check (`npm run format` fixes) |

## Notes

- `.npmrc` sets `legacy-peer-deps`: Expo SDK 57's optional peers (`react-dom`, `react-native-worklets`) conflict under npm's strict resolver.
- ESLint is pinned to 9: `eslint-plugin-react`, used by `eslint-config-expo`, does not run on ESLint 10 yet.
