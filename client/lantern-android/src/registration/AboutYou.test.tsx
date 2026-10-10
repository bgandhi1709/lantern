import '@testing-library/react-native/matchers';
import { fireEvent, render, screen, userEvent } from '@testing-library/react-native';
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

const regionField = () => screen.getByLabelText('Where do you live?');
const agreeBox = () => screen.getByRole('checkbox', { name: 'I agree to the privacy notice' });
const continueButton = () => screen.getByRole('button', { name: 'Continue' });

describe('Continue on About you', () => {
  it('stays a real disabled button until Region is filled and the notice is agreed', async () => {
    const user = userEvent.setup();
    await renderNotRegistered();
    expect(continueButton()).toBeDisabled();

    await user.type(regionField(), 'Pune, Maharashtra');
    expect(continueButton()).toBeDisabled();
    expect(agreeBox()).not.toBeChecked();

    await user.press(agreeBox());
    expect(agreeBox()).toBeChecked();
    expect(continueButton()).toBeEnabled();
  });

  it('works the other way round: agree first, then fill in Region', async () => {
    const user = userEvent.setup();
    await renderNotRegistered();

    await user.press(agreeBox());
    expect(continueButton()).toBeDisabled();

    await user.type(regionField(), 'Pune, Maharashtra');
    expect(continueButton()).toBeEnabled();
  });

  it('is disabled again when the agreement is taken back', async () => {
    const user = userEvent.setup();
    await renderNotRegistered();
    await user.type(regionField(), 'Pune, Maharashtra');
    await user.press(agreeBox());

    await user.press(agreeBox());

    expect(agreeBox()).not.toBeChecked();
    expect(continueButton()).toBeDisabled();
  });

  it.each([
    ['only spaces', '   '],
    ['a control character', 'Pune\u0007'],
  ])('stays disabled when Region is %s', async (_name, region) => {
    const user = userEvent.setup();
    await renderNotRegistered();
    await user.press(agreeBox());

    fireEvent.changeText(regionField(), region);

    expect(continueButton()).toBeDisabled();
  });

  it('stops Region at 60 characters', async () => {
    const user = userEvent.setup();
    await renderNotRegistered();

    await user.type(regionField(), 'x'.repeat(70));

    expect(regionField()).toHaveDisplayValue('x'.repeat(60));
  });
});
