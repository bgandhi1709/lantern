import { GoogleSignin } from '@react-native-google-signin/google-signin';

import { config } from '../shared/config';
import { endSession, startSession } from '../shared/session';

export class SignInError extends Error {
  constructor(readonly reason: 'cancelled' | 'failed') {
    super(reason);
  }
}

async function chooseGoogleAccount() {
  GoogleSignin.configure({ webClientId: config.googleWebClientId });
  try {
    return await GoogleSignin.signIn();
  } catch {
    throw new SignInError('failed');
  }
}

export async function signInWithGoogle() {
  const response = await chooseGoogleAccount();
  if (response.type === 'cancelled') throw new SignInError('cancelled');
  if (!response.data.idToken) throw new SignInError('failed');
  try {
    await startSession(response.data.idToken);
  } catch {
    throw new SignInError('failed');
  }
}

// Google keeps the chosen account, so forget it too; otherwise the next sign-in skips the account sheet.
export async function signOutOfLantern() {
  try {
    await GoogleSignin.signOut();
  } finally {
    await endSession();
  }
}
