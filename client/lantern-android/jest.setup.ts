import { resetFakeAuth } from './src/test/fakeFirebaseAuth';
import { resetFakeGoogleSignIn } from './src/test/fakeGoogleSignIn';
import { resetSecureStore } from './src/test/fakeSecureStore';

jest.mock('@react-native-async-storage/async-storage', () =>
  jest.requireActual('@react-native-async-storage/async-storage/jest/async-storage-mock'),
);
jest.mock('expo-secure-store', () => jest.requireActual('./src/test/fakeSecureStore'));
jest.mock('@react-native-firebase/auth', () => jest.requireActual('./src/test/fakeFirebaseAuth'));
jest.mock('@react-native-google-signin/google-signin', () =>
  jest.requireActual('./src/test/fakeGoogleSignIn'),
);
jest.mock('./src/shared/config', () => ({
  config: {
    apiBaseUrl: 'https://api.test',
    apiTimeoutMs: 1000,
    authEmulator: undefined,
    googleWebClientId: 'web-client-id',
  },
}));

beforeEach(() => {
  resetSecureStore();
  resetFakeAuth();
  resetFakeGoogleSignIn();
});
