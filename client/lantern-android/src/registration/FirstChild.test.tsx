import '@testing-library/react-native/matchers';
import { act, fireEvent, render, screen, userEvent } from '@testing-library/react-native';
import { BackHandler, Linking, Text } from 'react-native';

import { AuthGate, AuthProvider } from '../auth';
import { ApiError, api } from '../shared/api';
import { readFamilyKey } from '../shared/storage';
import { unlockText } from '../shared/crypto';
import { FAKE_UID, fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { RegistrationFlow } from '.';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const get = api.get as jest.Mock;
const post = api.post as jest.Mock;

const PASSPHRASE = 'blue lantern 2026';
const THIS_YEAR = new Date().getFullYear();

const finishButton = () => screen.getByRole('button', { name: /^(Finish|Finishing…|Try again)$/ });
const addButton = () => screen.queryByRole('button', { name: 'Add another child' });
const nameField = (n: number) => screen.getByTestId(`child-${n}-name`);

type User = ReturnType<typeof userEvent.setup>;

const hardwareBack = () => {
  const handlers: (() => boolean | null | undefined)[] = [];
  jest.spyOn(BackHandler, 'addEventListener').mockImplementation((_event, handler) => {
    const press = () => handler({ type: 'hardwareBackPress', timeStamp: 0 });
    handlers.push(press);
    return { remove: () => void handlers.splice(handlers.indexOf(press), 1) };
  });
  return () => handlers.at(-1)?.();
};

const renderChildStep = async (board: 'cbse' | 'ssc' = 'cbse') => {
  const user = userEvent.setup();
  fakeRestoredSession('restored-id-token');
  get.mockRejectedValue(new ApiError('not-registered', 404));
  await render(
    <AuthProvider>
      <AuthGate home={<Text>Home</Text>} registration={<RegistrationFlow />} />
    </AuthProvider>,
  );
  await user.type(await screen.findByLabelText('Where do you live?'), 'Pune, Maharashtra');
  await user.press(screen.getByRole('checkbox', { name: 'I agree to the privacy notice' }));
  await user.press(screen.getByRole('button', { name: 'Continue' }));
  await user.press(
    screen.getByRole('radio', { name: board === 'cbse' ? /^CBSE/ : /^Maharashtra/ }),
  );
  await user.press(screen.getByRole('button', { name: 'Continue' }));
  await user.type(screen.getByLabelText('Passphrase'), PASSPHRASE);
  await user.type(screen.getByLabelText('Type it again'), PASSPHRASE);
  await user.press(
    screen.getByRole('checkbox', {
      name: "I understand Lantern can't reset or change it, and I will keep it safe.",
    }),
  );
  await user.press(screen.getByRole('button', { name: 'Create passphrase' }));
  jest.spyOn(Linking, 'openURL').mockResolvedValue(true);
  await user.press(await screen.findByRole('button', { name: 'Email it to myself' }));
  await user.press(screen.getByRole('checkbox', { name: 'I have sent the email to myself.' }));
  await user.press(screen.getByRole('button', { name: 'Continue' }));
  await screen.findByRole('header', { name: 'Add your first child' });
  return user;
};

// The first Child picks the Class from buttons, an extra Child's card from a list.
const fillFirstChild = async (user: User, name = 'Aarav') => {
  await user.type(nameField(1), name);
  await user.press(screen.getByRole('radio', { name: 'Class 4' }));
  await fireEvent(screen.getAllByLabelText('Birth year')[0], 'valueChange', String(THIS_YEAR - 9));
};

const addExtra = async (user: User) =>
  user.press(screen.getByRole('button', { name: 'Add another child' }));

const fillExtra = async (user: User, n: number, name: string) => {
  await user.type(nameField(n), name);
  await fireEvent(screen.getByTestId(`child-${n}-class`), 'valueChange', '2');
  await fireEvent(screen.getByTestId(`child-${n}-year`), 'valueChange', String(THIS_YEAR - 6));
};

beforeEach(() => post.mockReset().mockResolvedValue({ familyId: 'a-family' }));

describe('the First child step', () => {
  it('sits at step 4 of 4 and keeps Finish a disabled button until the first Child is complete', async () => {
    const user = await renderChildStep();

    expect(screen.getByText('Step 4 of 4')).toBeOnTheScreen();
    expect(screen.getByText('One child is needed to start. You can add more below or later.'));
    expect(finishButton()).toBeDisabled();

    await user.type(nameField(1), 'Aarav');
    await user.press(screen.getByRole('radio', { name: 'Class 4' }));
    expect(finishButton()).toBeDisabled();

    await fireEvent(
      screen.getAllByLabelText('Birth year')[0],
      'valueChange',
      String(THIS_YEAR - 9),
    );
    expect(finishButton()).toBeEnabled();
  });

  it('offers Classes 1 to 10 for CBSE', async () => {
    await renderChildStep('cbse');

    expect(screen.getAllByRole('radio', { name: /^Class \d+$/ })).toHaveLength(10);
    expect(
      screen.queryByText('Classes 6 to 10 are not available on SSC yet.'),
    ).not.toBeOnTheScreen();
  });

  it('offers Classes 1 to 5 for SSC and says why not more', async () => {
    await renderChildStep('ssc');

    expect(screen.getAllByRole('radio', { name: /^Class \d+$/ })).toHaveLength(5);
    expect(screen.getByText('Classes 6 to 10 are not available on SSC yet.')).toBeOnTheScreen();
  });

  it('does not take a name of spaces alone', async () => {
    const user = await renderChildStep();
    await fillFirstChild(user, '   ');

    expect(finishButton()).toBeDisabled();
  });
});

describe('More children', () => {
  it('opens a card for each extra Child, named Another child until it has a name', async () => {
    const user = await renderChildStep();
    await fillFirstChild(user);
    await addExtra(user);

    expect(screen.getByText('Another child')).toBeOnTheScreen();
    expect(screen.getByText('Optional, skip if you like')).toBeOnTheScreen();
    expect(finishButton()).toBeEnabled();
  });

  it('ignores an extra Child left empty', async () => {
    const user = await renderChildStep();
    await fillFirstChild(user);
    await addExtra(user);
    await user.press(finishButton());

    expect(post.mock.calls[0][2].children).toHaveLength(1);
  });

  it('blocks Finish while an extra is half-filled, and says why once its card is closed', async () => {
    const user = await renderChildStep();
    await fillFirstChild(user);
    await addExtra(user);
    await user.type(nameField(2), 'Mira');

    expect(finishButton()).toBeDisabled();
    expect(
      screen.queryByText('Finish or remove the extra child to continue.'),
    ).not.toBeOnTheScreen();

    await user.press(screen.getByText('Mira'));

    expect(screen.getByText('Finish or remove the extra child to continue.')).toBeOnTheScreen();
    expect(screen.getByText('Needs name, class and birth year')).toBeOnTheScreen();
  });

  it('lets Finish go once the half-filled extra is removed, and names the child on the button', async () => {
    const user = await renderChildStep();
    await fillFirstChild(user);
    await addExtra(user);
    await user.type(nameField(2), 'Mira');

    await user.press(screen.getByRole('button', { name: 'Remove this child, Mira' }));

    expect(finishButton()).toBeEnabled();
    expect(
      screen.queryByText('Finish or remove the extra child to continue.'),
    ).not.toBeOnTheScreen();
  });

  it('says an extra is ready once it has a name, a Class and a birth year, and sends it with the rest', async () => {
    const user = await renderChildStep();
    await fillFirstChild(user);
    await addExtra(user);
    await fillExtra(user, 2, 'Mira');

    expect(screen.getByText('Class 2, ready to add')).toBeOnTheScreen();
    await user.press(finishButton());

    const children = post.mock.calls[0][2].children;
    expect(children).toHaveLength(2);
    expect(children[1].classLevel).toBe(2);
  });

  it('allows six Children in all, and no more', async () => {
    const user = await renderChildStep();
    await fillFirstChild(user);
    for (let n = 2; n <= 6; n += 1) await addExtra(user);

    expect(addButton()).not.toBeOnTheScreen();
  });
});

describe('Finish', () => {
  it('registers once with everything locked, keeps the Family key and lands on Home', async () => {
    const user = await renderChildStep();
    await fillFirstChild(user);
    await user.press(finishButton());

    expect(await screen.findByText('Home')).toBeOnTheScreen();
    expect(post).toHaveBeenCalledTimes(1);
    const [path, , body] = post.mock.calls[0];
    expect(path).toBe('/v1/register');
    const key = await readFamilyKey(FAKE_UID);
    expect(key).not.toBeNull();
    expect(unlockText(key as string, body.children[0].name)).toBe('Aarav');
    expect(JSON.stringify(body)).not.toContain('Aarav');
  });

  it('shows Finishing while it waits and takes a second press as one', async () => {
    let finishRequest: (value: unknown) => void = () => {};
    post.mockReturnValue(new Promise((resolve) => (finishRequest = resolve)));
    const user = await renderChildStep();
    await fillFirstChild(user);

    await user.press(finishButton());
    expect(screen.getByRole('button', { name: 'Finishing…' })).toBeDisabled();
    expect(screen.getByRole('progressbar', { name: 'Setting up your family…' })).toBeOnTheScreen();
    await user.press(screen.getByRole('button', { name: 'Finishing…' }));
    await act(async () => finishRequest({ familyId: 'a-family' }));

    expect(post).toHaveBeenCalledTimes(1);
    expect(await screen.findByText('Home')).toBeOnTheScreen();
  });

  it('goes straight to Home when the Family already exists, keeping no key', async () => {
    post.mockRejectedValue(new ApiError('already-registered', 409));
    const user = await renderChildStep();
    await fillFirstChild(user);
    await user.press(finishButton());

    expect(await screen.findByText('Home')).toBeOnTheScreen();
    expect(await readFamilyKey(FAKE_UID)).toBeNull();
  });

  it('tries again with a new Family id when the id is taken, without asking the Parent anything', async () => {
    post.mockRejectedValueOnce(new ApiError('family-id-taken', 409));
    const user = await renderChildStep();
    await fillFirstChild(user);
    await user.press(finishButton());

    expect(await screen.findByText('Home')).toBeOnTheScreen();
    expect(post).toHaveBeenCalledTimes(2);
    expect(post.mock.calls[1][2].familyId).not.toBe(post.mock.calls[0][2].familyId);
  });

  it('says a Class has no books yet, and lets the Parent press Try again', async () => {
    post.mockRejectedValueOnce(new ApiError('class-not-available', 400));
    const user = await renderChildStep();
    await fillFirstChild(user);
    await user.press(finishButton());

    expect(
      await screen.findByText(
        "Lantern doesn't have books for one of these Classes on this board yet. Check each child's Class, then try again.",
      ),
    ).toBeOnTheScreen();
    expect(await readFamilyKey(FAKE_UID)).toBeNull();

    await user.press(screen.getByRole('button', { name: 'Try again' }));
    expect(await screen.findByText('Home')).toBeOnTheScreen();
  });

  it("ignores Android's Back while Finish runs, and goes back to the recovery code otherwise", async () => {
    const back = hardwareBack();
    let finishRequest: (value: unknown) => void = () => {};
    post.mockReturnValue(new Promise((resolve) => (finishRequest = resolve)));
    const user = await renderChildStep();
    await fillFirstChild(user);

    await user.press(finishButton());
    await act(async () => void back());
    expect(screen.getByRole('button', { name: 'Finishing…' })).toBeOnTheScreen();

    await act(async () => finishRequest({ familyId: 'a-family' }));
    expect(await screen.findByText('Home')).toBeOnTheScreen();
  });

  it("takes Android's Back to the recovery code when nothing is running", async () => {
    const back = hardwareBack();
    const user = await renderChildStep();
    await fillFirstChild(user);

    await act(async () => void back());

    expect(
      await screen.findByRole('header', { name: 'Save your recovery code' }),
    ).toBeOnTheScreen();
  });

  it('clears the failure message as soon as the Parent changes an entry', async () => {
    post.mockRejectedValueOnce(new ApiError('class-not-available', 400));
    const user = await renderChildStep();
    await fillFirstChild(user);
    await user.press(finishButton());
    await screen.findByRole('button', { name: 'Try again' });

    await user.type(nameField(1), 'a');

    expect(screen.queryByText(/books for one of these Classes/)).not.toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Finish' })).toBeEnabled();
  });

  it('says it could not reach Lantern when there is no connection', async () => {
    post.mockRejectedValueOnce(new ApiError('network', null));
    const user = await renderChildStep();
    await fillFirstChild(user);
    await user.press(finishButton());

    expect(
      await screen.findByText("Couldn't reach Lantern. Check your connection, then try again."),
    ).toBeOnTheScreen();
  });

  it('says plainly that something went wrong for any other rejection', async () => {
    post.mockRejectedValueOnce(new ApiError('invalid-registration', 400));
    const user = await renderChildStep();
    await fillFirstChild(user);
    await user.press(finishButton());

    expect(
      await screen.findByText(
        'Something went wrong, and your family was not set up. Please try again.',
      ),
    ).toBeOnTheScreen();
  });
});
