import '@testing-library/react-native/matchers';
import { act, render, screen, userEvent } from '@testing-library/react-native';
import { BackHandler, Text } from 'react-native';

import { ApiError, api } from '../shared/api';
import { fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { GoogleSignin } from '../test/fakeGoogleSignIn';
import { AuthGate, AuthProvider } from '../auth';
import { RegistrationFlow } from '.';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const get = api.get as jest.Mock;

const regionField = () => screen.getByLabelText('Where do you live?');
const agreeBox = () => screen.getByRole('checkbox', { name: 'I agree to the privacy notice' });
const backButton = () => screen.getByRole('button', { name: 'Back' });
const continueButton = () => screen.getByRole('button', { name: 'Continue' });
const cbseCard = () => screen.getByRole('radio', { name: /^CBSE/ });
const sscCard = () => screen.getByRole('radio', { name: /^Maharashtra State Board/ });

const renderBoardStep = async () => {
  const user = userEvent.setup();
  fakeRestoredSession('restored-id-token');
  get.mockRejectedValue(new ApiError('not-registered', 404));
  await render(
    <AuthProvider>
      <AuthGate home={<Text>Home</Text>} registration={<RegistrationFlow />} />
    </AuthProvider>,
  );
  await user.type(await screen.findByLabelText('Where do you live?'), 'Pune, Maharashtra');
  await user.press(agreeBox());
  await user.press(continueButton());
  return user;
};

describe('the Board step', () => {
  it('asks which board the child follows, says it cannot be changed, and sits at step 2 of 4', async () => {
    await renderBoardStep();

    expect(
      screen.getByRole('header', { name: 'Which board does your child follow?' }),
    ).toBeOnTheScreen();
    expect(
      screen.getByText("This decides which school books Lantern uses. You can't change it later."),
    ).toBeOnTheScreen();
    expect(screen.getByText('Step 2 of 4')).toBeOnTheScreen();
  });

  it('offers CBSE and the Maharashtra State Board as two cards, each with its books and Classes', async () => {
    await renderBoardStep();

    expect(screen.getByLabelText('Board')).toBeOnTheScreen();
    expect(screen.getAllByRole('radio')).toHaveLength(2);
    expect(screen.getByText('NCERT books')).toBeOnTheScreen();
    expect(screen.getByText('Classes 1 to 10')).toBeOnTheScreen();
    expect(screen.getByText('SSC · Balbharati books, English medium')).toBeOnTheScreen();
    expect(screen.getByText('Classes 1 to 5')).toBeOnTheScreen();
  });

  it('preselects nothing, so Continue is a real disabled button until a Board is picked', async () => {
    await renderBoardStep();

    expect(cbseCard()).not.toBeChecked();
    expect(sscCard()).not.toBeChecked();
    expect(continueButton()).toBeDisabled();
  });

  it('selects the card that is tapped, and enables Continue', async () => {
    const user = await renderBoardStep();

    await user.press(cbseCard());

    expect(cbseCard()).toBeChecked();
    expect(sscCard()).not.toBeChecked();
    expect(continueButton()).toBeEnabled();
  });

  it('lets the Parent change their pick before Continue, one card selected at a time', async () => {
    const user = await renderBoardStep();
    await user.press(cbseCard());

    await user.press(sscCard());

    expect(sscCard()).toBeChecked();
    expect(cbseCard()).not.toBeChecked();
  });

  it('shows a tick on the selected card only, not on colour alone', async () => {
    const user = await renderBoardStep();
    expect(screen.queryByTestId('board-tick')).not.toBeOnTheScreen();

    await user.press(sscCard());

    expect(screen.getAllByTestId('board-tick')).toHaveLength(1);
    expect(sscCard()).toContainElement(screen.getByTestId('board-tick'));
  });

  it('keeps the pick when the Parent goes back to About you and forward again', async () => {
    const user = await renderBoardStep();
    await user.press(sscCard());

    await user.press(backButton());
    expect(regionField()).toHaveDisplayValue('Pune, Maharashtra');
    await user.press(continueButton());

    expect(sscCard()).toBeChecked();
    expect(continueButton()).toBeEnabled();
  });

  it('goes to step 3 on Continue, and Back returns to the Board with the pick kept', async () => {
    const user = await renderBoardStep();
    await user.press(cbseCard());

    await user.press(continueButton());

    expect(screen.getByText('Step 3 of 4')).toBeOnTheScreen();
    expect(screen.getByText('Passphrase comes next')).toBeOnTheScreen();

    await user.press(backButton());

    expect(
      screen.getByRole('header', { name: 'Which board does your child follow?' }),
    ).toBeOnTheScreen();
    expect(cbseCard()).toBeChecked();
  });

  it("takes Android's back button back one step at a time", async () => {
    const handlers: (() => boolean | null | undefined)[] = [];
    jest.spyOn(BackHandler, 'addEventListener').mockImplementation((_event, handler) => {
      const press = () => handler({ type: 'hardwareBackPress', timeStamp: 0 });
      handlers.push(press);
      return { remove: () => void handlers.splice(handlers.indexOf(press), 1) };
    });
    const user = await renderBoardStep();
    await user.press(cbseCard());
    await user.press(continueButton());

    await act(() => void handlers.at(-1)?.());
    expect(
      screen.getByRole('header', { name: 'Which board does your child follow?' }),
    ).toBeOnTheScreen();

    await act(() => void handlers.at(-1)?.());
    expect(screen.getByRole('header', { name: 'A little about you' })).toBeOnTheScreen();
  });

  it('drops the pick when the Parent signs out, so the next sign-in starts with nothing chosen', async () => {
    const user = await renderBoardStep();
    await user.press(cbseCard());
    await user.press(backButton());

    await user.press(backButton());
    await user.press(await screen.findByRole('button', { name: 'Continue with Google' }));
    await user.type(await screen.findByLabelText('Where do you live?'), 'Pune');
    await user.press(agreeBox());
    await user.press(continueButton());

    expect(GoogleSignin.signOut).toHaveBeenCalled();
    expect(cbseCard()).not.toBeChecked();
    expect(continueButton()).toBeDisabled();
  });
});
