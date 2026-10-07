import { AccessibilityInfo } from 'react-native';

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
  // Screens appear at once in tests; the Opening tests turn the intro animation back on.
  jest.spyOn(AccessibilityInfo, 'isReduceMotionEnabled').mockResolvedValue(true);
  resetSecureStore();
  resetFakeAuth();
  resetFakeGoogleSignIn();
});
