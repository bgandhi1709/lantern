import '@testing-library/react-native/matchers';
import { render, screen, userEvent } from '@testing-library/react-native';

import { ApiError, api } from '../shared/api';
import { fakeRestoredSession, signOut as firebaseSignOut } from '../test/fakeFirebaseAuth';
import { GoogleSignin } from '../test/fakeGoogleSignIn';
import { AuthGate, AuthProvider, SignedInPlaceholder } from '.';
import { signOutOfLantern } from './googleSignIn';

jest.mock('../shared/api', () => ({
  ApiError: jest.requireActual('../shared/api/ApiError').ApiError,
  api: { get: jest.fn() },
}));

const get = api.get as jest.Mock;

const renderSignedIn = () => {
  fakeRestoredSession();
  return render(
    <AuthProvider>
      <AuthGate signedIn={<SignedInPlaceholder />} />
    </AuthProvider>,
  );
};

describe('the signed-in placeholder', () => {
  it('asks the API who the Parent is and says a registered Family is known', async () => {
    get.mockResolvedValue({});

    await renderSignedIn();

    expect(await screen.findByText('Signed in. Lantern knows your Family.')).toBeOnTheScreen();
    expect(get.mock.calls[0][0]).toBe('/v1/me');
  });

  it('says so when the Parent has not registered yet', async () => {
    get.mockRejectedValue(new ApiError('not-registered', 404));

    await renderSignedIn();

    expect(await screen.findByText(/not registered a Family yet/)).toBeOnTheScreen();
  });

  it('says it cannot reach Lantern, and checks again on request', async () => {
    get.mockRejectedValueOnce(new ApiError('network', null)).mockResolvedValue({});
    const user = userEvent.setup();
    await renderSignedIn();
    expect(await screen.findByText("Can't reach Lantern")).toBeOnTheScreen();

    await user.press(screen.getByRole('button', { name: 'Check again' }));

    expect(await screen.findByText('Signed in. Lantern knows your Family.')).toBeOnTheScreen();
  });

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
