import '@testing-library/react-native/matchers';
import { render, screen } from '@testing-library/react-native';
import { Text } from 'react-native';

import { ApiError, api } from '../shared/api';
import { fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { AuthGate, AuthProvider } from '../auth';
import { RegistrationFlow } from '.';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const get = api.get as jest.Mock;

const renderNotRegistered = async (profile?: { displayName: string | null; email: string }) => {
  fakeRestoredSession('restored-id-token', profile);
  get.mockRejectedValue(new ApiError('not-registered', 404));
  await render(
    <AuthProvider>
      <AuthGate home={<Text>Home</Text>} registration={<RegistrationFlow />} />
    </AuthProvider>,
  );
};

describe('About you, the first registration step', () => {
  it('is where a signed-in Parent who has not registered lands, with the Google name and email', async () => {
    await renderNotRegistered();

    expect(await screen.findByRole('header', { name: 'A little about you' })).toBeOnTheScreen();
    expect(screen.getByText('Step 1 of 4')).toBeOnTheScreen();
    expect(screen.getByText('Asha Verma')).toBeOnTheScreen();
    expect(screen.getByText('asha.verma@gmail.com')).toBeOnTheScreen();
    expect(screen.queryByDisplayValue('Asha Verma')).not.toBeOnTheScreen();
  });
});
