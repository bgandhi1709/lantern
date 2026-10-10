import '@testing-library/react-native/matchers';
import { render, screen, userEvent } from '@testing-library/react-native';
import { Text } from 'react-native';

import { failTokenRefresh, fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { jsonResponse } from '../test/fakeApi';
import { AuthGate, AuthProvider } from '.';

const renderApp = () =>
  render(
    <AuthProvider>
      <AuthGate home={<Text>Home</Text>} />
    </AuthProvider>,
  );

describe('a saved sign-in that Firebase no longer accepts', () => {
  beforeEach(() => {
    global.fetch = jest.fn(async () => jsonResponse(200, {})) as typeof fetch;
  });

  it('sends the Parent to Start instead of leaving them on Try again', async () => {
    fakeRestoredSession();
    failTokenRefresh('auth/invalid-refresh');

    await renderApp();

    expect(await screen.findByRole('button', { name: 'Continue with Google' })).toBeOnTheScreen();
    expect(screen.queryByText("Can't reach Lantern")).not.toBeOnTheScreen();
  });

  it('still says Lantern is unreachable when only the network is down', async () => {
    fakeRestoredSession();
    failTokenRefresh('auth/network-request-failed');
    const user = userEvent.setup();

    await renderApp();

    expect(await screen.findByText("Can't reach Lantern")).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeOnTheScreen();
    await user.press(screen.getByRole('button', { name: 'Try again' }));
  });
});
