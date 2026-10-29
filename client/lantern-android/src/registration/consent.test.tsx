import '@testing-library/react-native/matchers';
import { render, screen, userEvent } from '@testing-library/react-native';
import { Text } from 'react-native';

import { AuthGate, AuthProvider, signOutOfLantern, useRegistrationCheck } from '../auth';
import { setCollectionEnabled, useTrackScreen } from '../shared/analytics';
import { ApiError, api } from '../shared/api';
import { fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { logScreenView } from '../test/fakeFirebaseAnalytics';
import { performance } from '../test/fakeFirebasePerf';
import { RegistrationFlow } from '.';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const get = api.get as jest.Mock;

function Controls() {
  const { recheck } = useRegistrationCheck();
  return (
    <>
      <Text onPress={signOutOfLantern}>Sign out</Text>
      <Text onPress={recheck}>Family deleted</Text>
    </>
  );
}

function Home() {
  useTrackScreen('home');
  return <Text>Home</Text>;
}

const renderApp = async () => {
  fakeRestoredSession();
  await render(
    <AuthProvider>
      <Controls />
      <AuthGate home={<Home />} registration={<RegistrationFlow />} />
    </AuthProvider>,
  );
};

const collecting = () => performance.dataCollectionEnabled;
const agreeBox = () => screen.getByRole('checkbox', { name: 'I agree to the privacy notice' });

beforeEach(() => setCollectionEnabled(false));

describe('when anonymous usage numbers are collected', () => {
  it('is not before a new Parent has agreed to the notice', async () => {
    get.mockRejectedValue(new ApiError('not-registered', 404));

    await renderApp();
    await screen.findByLabelText('Where do you live?');

    expect(collecting()).toBe(false);
  });

  it('starts when they tick the notice and stops when they take the tick back', async () => {
    const user = userEvent.setup();
    get.mockRejectedValue(new ApiError('not-registered', 404));
    await renderApp();
    await screen.findByLabelText('Where do you live?');

    await user.press(agreeBox());
    expect(collecting()).toBe(true);

    await user.press(agreeBox());
    expect(collecting()).toBe(false);
  });

  it('stops when a Parent who agreed leaves registration', async () => {
    const user = userEvent.setup();
    get.mockRejectedValue(new ApiError('not-registered', 404));
    await renderApp();
    await user.press(
      await screen.findByRole('checkbox', { name: 'I agree to the privacy notice' }),
    );

    await user.press(screen.getByText('Sign out'));

    expect(await screen.findByRole('button', { name: 'Continue with Google' })).toBeOnTheScreen();
    expect(collecting()).toBe(false);
  });

  it('runs for a registered Parent, who agreed when registering', async () => {
    get.mockResolvedValue({});

    await renderApp();
    await screen.findByText('Home');

    expect(collecting()).toBe(true);
  });

  it('counts the first screen a registered Parent sees, not only the ones after it', async () => {
    get.mockResolvedValue({});

    await renderApp();
    await screen.findByText('Home');

    expect(logScreenView).toHaveBeenCalledWith(expect.anything(), {
      screen_name: 'home',
      screen_class: 'home',
    });
  });

  it('stops when a registered Parent signs out', async () => {
    const user = userEvent.setup();
    get.mockResolvedValue({});
    await renderApp();
    await screen.findByText('Home');

    await user.press(screen.getByText('Sign out'));

    expect(await screen.findByRole('button', { name: 'Continue with Google' })).toBeOnTheScreen();
    expect(collecting()).toBe(false);
  });

  it('stops when the Family is deleted, until the Parent agrees again', async () => {
    const user = userEvent.setup();
    get.mockResolvedValueOnce({}).mockRejectedValueOnce(new ApiError('not-registered', 404));
    await renderApp();
    await screen.findByText('Home');

    await user.press(screen.getByText('Family deleted'));
    await screen.findByLabelText('Where do you live?');

    expect(collecting()).toBe(false);
  });
});
