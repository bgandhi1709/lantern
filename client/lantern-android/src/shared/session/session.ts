import {
  GoogleAuthProvider,
  connectAuthEmulator,
  getAuth,
  onAuthStateChanged,
  signInWithCredential,
  signOut,
} from '@react-native-firebase/auth';

import { config } from '../config';

const auth = getAuth();

if (config.authEmulator) connectAuthEmulator(auth, `http://${config.authEmulator}`);

// Firebase says these when the saved sign-in is gone for good (revoked, expired or the account was
// removed), unlike a network failure, which the Parent can retry.
const REVOKED_CODES = new Set([
  'auth/invalid-refresh',
  'auth/user-token-expired',
  'auth/invalid-user-token',
  'auth/user-not-found',
  'auth/user-disabled',
]);

const isRevoked = (error: unknown) =>
  typeof error === 'object' &&
  error !== null &&
  'code' in error &&
  typeof error.code === 'string' &&
  REVOKED_CODES.has(error.code);

// Firebase renews the ID token itself, so asking for it before each call is all the app does.
export async function getIdToken() {
  try {
    return (await auth.currentUser?.getIdToken()) ?? null;
  } catch (error) {
    if (isRevoked(error)) await endSession();
    throw error;
  }
}

export type Parent = { uid: string; name: string; email: string };

// The name and email on the Google account. Google always sends an email; the name falls back to it.
export function getParent(): Parent {
  const email = auth.currentUser?.email ?? '';
  return { uid: auth.currentUser?.uid ?? '', name: auth.currentUser?.displayName || email, email };
}

export function onSessionChange(listener: (signedIn: boolean) => void) {
  return onAuthStateChanged(auth, (user) => listener(user !== null));
}

export async function startSession(googleIdToken: string) {
  await signInWithCredential(auth, GoogleAuthProvider.credential(googleIdToken));
}

export async function endSession() {
  await signOut(auth);
}
