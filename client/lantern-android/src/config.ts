const apiBaseUrl = process.env.EXPO_PUBLIC_API_BASE_URL;

if (!apiBaseUrl) {
  throw new Error('EXPO_PUBLIC_API_BASE_URL is not set. Copy .env.example to .env and fill it in.');
}

if (!__DEV__ && !apiBaseUrl.startsWith('https://')) {
  throw new Error('EXPO_PUBLIC_API_BASE_URL must be an https:// address in a release build.');
}

export const config = { apiBaseUrl: apiBaseUrl.replace(/\/+$/, '') };
