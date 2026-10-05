import { resetSecureStore } from './src/test/fakeSecureStore';

jest.mock('@react-native-async-storage/async-storage', () =>
  jest.requireActual('@react-native-async-storage/async-storage/jest/async-storage-mock'),
);
jest.mock('expo-secure-store', () => jest.requireActual('./src/test/fakeSecureStore'));

beforeEach(() => {
  resetSecureStore();
});
