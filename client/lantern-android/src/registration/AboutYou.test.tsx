import '@testing-library/react-native/matchers';
import { act, fireEvent, render, screen, userEvent } from '@testing-library/react-native';
import { BackHandler, Text } from 'react-native';

import { ApiError, api } from '../shared/api';
import { fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { GoogleSignin } from '../test/fakeGoogleSignIn';
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

const fillIn = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.type(regionField(), 'Pune, Maharashtra');
  await user.press(agreeBox());
};
const backButton = () => screen.getByRole('button', { name: 'Back' });

describe('moving between the steps', () => {
  const handlers: (() => boolean | null | undefined)[] = [];
  const pressSystemBack = () => act(() => void handlers.at(-1)?.());

  beforeEach(() => {
    handlers.length = 0;
    jest.spyOn(BackHandler, 'addEventListener').mockImplementation((_event, handler) => {
      const press = () => handler({ type: 'hardwareBackPress', timeStamp: 0 });
      handlers.push(press);
      return { remove: () => void handlers.splice(handlers.indexOf(press), 1) };
    });
  });

  it('opens the privacy notice from its link and comes back with everything kept', async () => {
    const user = userEvent.setup();
    await renderNotRegistered();
    await fillIn(user);

    await user.press(screen.getByRole('link', { name: 'privacy notice' }));

    expect(screen.getByRole('header', { name: 'Your privacy, in plain words' })).toBeOnTheScreen();
    expect(screen.getByText('Notice version 2026-10')).toBeOnTheScreen();
    expect(screen.queryByText('Step 1 of 4')).not.toBeOnTheScreen();

    await user.press(backButton());

    expect(regionField()).toHaveDisplayValue('Pune, Maharashtra');
    expect(agreeBox()).toBeChecked();
  });

  it('goes to step 2 on Continue, and Back returns with everything kept', async () => {
    const user = userEvent.setup();
    await renderNotRegistered();
    await fillIn(user);

    await user.press(continueButton());

    expect(screen.getByText('Step 2 of 4')).toBeOnTheScreen();
    expect(screen.getByText('Board comes next')).toBeOnTheScreen();
    expect(screen.queryByRole('header', { name: 'A little about you' })).not.toBeOnTheScreen();

    await user.press(backButton());

    expect(regionField()).toHaveDisplayValue('Pune, Maharashtra');
    expect(agreeBox()).toBeChecked();
    expect(continueButton()).toBeEnabled();
  });

  it("takes Android's back button back one step, and leaves it to the system on About you", async () => {
    const user = userEvent.setup();
    await renderNotRegistered();
    expect(handlers).toHaveLength(0);
    await fillIn(user);
    await user.press(continueButton());

    await pressSystemBack();

    expect(screen.getByRole('header', { name: 'A little about you' })).toBeOnTheScreen();
    expect(regionField()).toHaveDisplayValue('Pune, Maharashtra');
    expect(handlers).toHaveLength(0);
  });
});

describe('leaving registration', () => {
  it('signs the Parent out and shows Start when they tap Back on step 1', async () => {
    const user = userEvent.setup();
    await renderNotRegistered();

    await user.press(backButton());

    expect(await screen.findByRole('button', { name: 'Continue with Google' })).toBeOnTheScreen();
    expect(GoogleSignin.signOut).toHaveBeenCalled();
    expect(screen.queryByRole('header', { name: 'A little about you' })).not.toBeOnTheScreen();
  });

  it('drops what was typed, so the next sign-in starts with an empty draft', async () => {
    const user = userEvent.setup();
    await renderNotRegistered();
    await fillIn(user);

    await user.press(backButton());
    await user.press(await screen.findByRole('button', { name: 'Continue with Google' }));

    expect(await screen.findByLabelText('Where do you live?')).toHaveDisplayValue('');
    expect(agreeBox()).not.toBeChecked();
    expect(continueButton()).toBeDisabled();
  });

  it('shows the email in place of a name when Google has none', async () => {
    await renderNotRegistered({ displayName: null, email: 'asha.verma@gmail.com' });

    expect(await screen.findAllByText('asha.verma@gmail.com')).toHaveLength(2);
    expect(screen.queryByText('Asha Verma')).not.toBeOnTheScreen();
  });
});
