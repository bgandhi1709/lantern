const apiBaseUrl = process.env.EXPO_PUBLIC_API_BASE_URL;

if (!apiBaseUrl) {
  throw new Error('EXPO_PUBLIC_API_BASE_URL is not set. Copy .env.example to .env and fill it in.');
}

if (!__DEV__ && !apiBaseUrl.startsWith('https://')) {
  throw new Error('EXPO_PUBLIC_API_BASE_URL must be an https:// address in a release build.');
}

// UAT scales to zero: the first call after idle takes about 25 s.
const API_TIMEOUT_MS = 45_000;

export const config = { apiBaseUrl: apiBaseUrl.replace(/\/+$/, ''), apiTimeoutMs: API_TIMEOUT_MS };
