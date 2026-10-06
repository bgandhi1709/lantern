import {
  GoogleAuthProvider,
  connectAuthEmulator,
  getAuth,
  onAuthStateChanged,
  signInWithCredential,
  signOut,
} from '@react-native-firebase/auth';

import { config } from '../config';
import { clearAll } from '../storage';

const auth = getAuth();

if (config.authEmulator) connectAuthEmulator(auth, `http://${config.authEmulator}`);

// Firebase renews the ID token itself, so asking for it before each call is all the app does.
export async function getIdToken() {
  return (await auth.currentUser?.getIdToken()) ?? null;
}

export function onSessionChange(listener: (signedIn: boolean) => void) {
  return onAuthStateChanged(auth, (user) => listener(user !== null));
}

export async function startSession(googleIdToken: string) {
  await signInWithCredential(auth, GoogleAuthProvider.credential(googleIdToken));
}

export async function endSession() {
  try {
    await signOut(auth);
  } finally {
    await clearAll();
  }
}
