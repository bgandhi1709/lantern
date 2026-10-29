import { AccessibilityInfo } from 'react-native';

import { resetFakeAnalytics } from './src/test/fakeFirebaseAnalytics';
import { resetFakeAuth } from './src/test/fakeFirebaseAuth';
import { resetFakePerf } from './src/test/fakeFirebasePerf';
import { resetFakeGoogleSignIn } from './src/test/fakeGoogleSignIn';
import { resetSecureStore } from './src/test/fakeSecureStore';

jest.mock('@react-native-async-storage/async-storage', () =>
  jest.requireActual('@react-native-async-storage/async-storage/jest/async-storage-mock'),
);
jest.mock('expo-secure-store', () => jest.requireActual('./src/test/fakeSecureStore'));
jest.mock('react-native-quick-crypto', () => jest.requireActual('./src/test/fakeQuickCrypto'));
jest.mock('@react-native-firebase/auth', () => jest.requireActual('./src/test/fakeFirebaseAuth'));
jest.mock('@react-native-firebase/analytics', () =>
  jest.requireActual('./src/test/fakeFirebaseAnalytics'),
);
jest.mock('@react-native-firebase/perf', () => jest.requireActual('./src/test/fakeFirebasePerf'));
jest.mock('@react-native-google-signin/google-signin', () =>
  jest.requireActual('./src/test/fakeGoogleSignIn'),
);
// Screens are tested outside a navigator, so every screen counts as the one in front.
jest.mock('expo-router', () => ({ useIsFocused: () => true }));
jest.mock('./src/shared/config', () => ({
  config: {
    apiBaseUrl: 'https://api.test',
    apiTimeoutMs: 1000,
    authEmulator: undefined,
    googleWebClientId: 'web-client-id',
    storybookEnabled: false,
  },
}));

beforeEach(() => {
  // Screens appear at once in tests; the Opening tests turn the intro animation back on.
  jest.spyOn(AccessibilityInfo, 'isReduceMotionEnabled').mockResolvedValue(true);
  resetSecureStore();
  resetFakeAuth();
  resetFakeAnalytics();
  resetFakePerf();
  resetFakeGoogleSignIn();
});
