import '@testing-library/react-native/matchers';
import { render, screen, userEvent } from '@testing-library/react-native';

import { api } from '../shared/api';
import { fakeRestoredSession, signOut as firebaseSignOut } from '../test/fakeFirebaseAuth';
import { GoogleSignin } from '../test/fakeGoogleSignIn';
import { AuthGate, AuthProvider, SignedInPlaceholder } from '.';
import { signOutOfLantern } from './googleSignIn';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const get = api.get as jest.Mock;

const renderSignedIn = () => {
  fakeRestoredSession();
  return render(
    <AuthProvider>
      <AuthGate home={<SignedInPlaceholder />} />
    </AuthProvider>,
  );
};

describe('the signed-in placeholder', () => {
  it('signs out of Firebase and Google and returns to Start', async () => {
    get.mockResolvedValue({});
    const user = userEvent.setup();
    await renderSignedIn();

    await user.press(await screen.findByRole('button', { name: 'Sign out' }));

    expect(await screen.findByRole('button', { name: 'Continue with Google' })).toBeOnTheScreen();
    expect(GoogleSignin.signOut).toHaveBeenCalled();
  });

  it('signs out cleanly after the app restarts, when Google was never configured', async () => {
    fakeRestoredSession();

    await expect(signOutOfLantern()).resolves.toBeUndefined();

    expect(GoogleSignin.signOut).toHaveBeenCalled();
    expect(firebaseSignOut).toHaveBeenCalled();
  });

  it('still ends the Lantern session when Google cannot sign out', async () => {
    fakeRestoredSession();
    GoogleSignin.signOut.mockRejectedValueOnce(new Error('offline'));

    await expect(signOutOfLantern()).resolves.toBeUndefined();

    expect(firebaseSignOut).toHaveBeenCalled();
  });
});
