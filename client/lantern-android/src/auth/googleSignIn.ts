import { GoogleSignin } from '@react-native-google-signin/google-signin';

import { config } from '../shared/config';
import { endSession, startSession } from '../shared/session';

export type SignInFailure = 'cancelled' | 'failed';

export class SignInError extends Error {
  constructor(readonly reason: SignInFailure) {
    super(reason);
  }
}

// The native module must be configured in every app run, including one that starts already signed in.
const configureGoogle = () => GoogleSignin.configure({ webClientId: config.googleWebClientId });

async function chooseGoogleAccount() {
  configureGoogle();
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

// Ending the Lantern session is what signs the Parent out. Forgetting the Google account as well
// makes the next sign-in show the account sheet; if Google cannot, the Parent is still signed out.
export async function signOutOfLantern() {
  try {
    configureGoogle();
    await GoogleSignin.signOut();
  } catch {
    // Best effort: the account sheet may be skipped next time.
  }
  await endSession();
}
