import '@testing-library/react-native/matchers';
import { render, screen, userEvent } from '@testing-library/react-native';
import { Text } from 'react-native';

import { fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { GoogleSignin, setGoogleOutcome } from '../test/fakeGoogleSignIn';
import { AuthGate, AuthProvider } from '.';

const renderApp = () =>
  render(
    <AuthProvider>
      <AuthGate signedIn={<Text>Home</Text>} />
    </AuthProvider>,
  );

const googleButton = () => screen.getByRole('button', { name: 'Continue with Google' });

describe('the Start screen', () => {
  it('speaks to the Parent and offers one Continue with Google button', async () => {
    await renderApp();

    expect(
      screen.getByRole('header', { name: 'Teach your child with confidence.' }),
    ).toBeOnTheScreen();
    expect(screen.getByText('Teach a chapter')).toBeOnTheScreen();
    expect(screen.getByText('Answer their questions')).toBeOnTheScreen();
    expect(screen.getByText('See their progress')).toBeOnTheScreen();
    expect(screen.getByText('Free and non-commercial.')).toBeOnTheScreen();
    expect(
      screen.getByRole('image', { name: 'A mother teaching her child from an open book' }),
    ).toBeOnTheScreen();
    expect(googleButton()).toBeEnabled();
  });

  it('signs the Parent in and leaves Start', async () => {
    const user = userEvent.setup();
    await renderApp();

    await user.press(googleButton());

    expect(await screen.findByText('Home')).toBeOnTheScreen();
    expect(screen.queryByText('Continue with Google')).not.toBeOnTheScreen();
  });

  it('is disabled and says so while signing in', async () => {
    GoogleSignin.signIn.mockReturnValueOnce(new Promise(() => {}));
    const user = userEvent.setup();
    await renderApp();

    await user.press(googleButton());

    expect(screen.getByRole('button', { name: 'Signing in…' })).toBeDisabled();
  });

  it('stays quietly on Start when the Parent closes the account sheet', async () => {
    setGoogleOutcome({ type: 'cancelled', data: null });
    const user = userEvent.setup();
    await renderApp();

    await user.press(googleButton());

    expect(googleButton()).toBeEnabled();
    expect(screen.queryByText(/Couldn't sign in/)).not.toBeOnTheScreen();
  });

  it('says it failed, and lets the Parent try again', async () => {
    setGoogleOutcome(new Error('network'));
    const user = userEvent.setup();
    await renderApp();

    await user.press(googleButton());
    expect(await screen.findByText("Couldn't sign in. Try again.")).toBeOnTheScreen();

    setGoogleOutcome({ type: 'success', data: { idToken: 'google-id-token' } });
    await user.press(googleButton());
    expect(await screen.findByText('Home')).toBeOnTheScreen();
  });

  it('fails when Google gives no ID token', async () => {
    setGoogleOutcome({ type: 'success', data: { idToken: null } });
    const user = userEvent.setup();
    await renderApp();

    await user.press(googleButton());

    expect(await screen.findByText("Couldn't sign in. Try again.")).toBeOnTheScreen();
  });

  it('skips Start for a Parent whose session is restored', async () => {
    fakeRestoredSession();

    await renderApp();

    expect(await screen.findByText('Home')).toBeOnTheScreen();
    expect(screen.queryByText('Continue with Google')).not.toBeOnTheScreen();
  });
});
