import Constants from 'expo-constants';

const apiBaseUrl = process.env.EXPO_PUBLIC_API_BASE_URL;
const authEmulator = process.env.EXPO_PUBLIC_FIREBASE_AUTH_EMULATOR || undefined;
const googleWebClientId = Constants.expoConfig?.extra?.googleWebClientId;

if (!apiBaseUrl) {
  throw new Error('EXPO_PUBLIC_API_BASE_URL is not set. Copy .env.example to .env and fill it in.');
}

if (!__DEV__ && !apiBaseUrl.startsWith('https://')) {
  throw new Error('EXPO_PUBLIC_API_BASE_URL must be an https:// address in a release build.');
}

if (!__DEV__ && authEmulator) {
  throw new Error('EXPO_PUBLIC_FIREBASE_AUTH_EMULATOR must not be set in a release build.');
}

if (typeof googleWebClientId !== 'string') {
  throw new Error(
    'google-services.json has no web client. Register the app in Firebase and rebuild.',
  );
}

// UAT scales to zero: the first call after idle takes about 25 s.
const API_TIMEOUT_MS = 45_000;

export const config = {
  apiBaseUrl: apiBaseUrl.replace(/\/+$/, ''),
  apiTimeoutMs: API_TIMEOUT_MS,
  authEmulator,
  googleWebClientId,
};
