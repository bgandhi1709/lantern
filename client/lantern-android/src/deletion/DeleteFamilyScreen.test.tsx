import '@testing-library/react-native/matchers';
import { act, render, screen, userEvent } from '@testing-library/react-native';

import { ApiError, api } from '../shared/api';
import { clearAll, readValue, writeValue } from '../shared/storage';
import { answerLikeTheApi, registerFamilyOnThisPhone } from '../test/fakeFamily';
import { fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { DeleteFamilyScreen } from './DeleteFamilyScreen';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const get = api.get as jest.Mock;
const post = api.post as jest.Mock;
const remove = api.delete as jest.Mock;
const onDeleted = jest.fn();
const onBack = jest.fn();

const PROOF = 'proof-for-the-family';
const CHALLENGE = 'challenge-for-the-family';
const REQUEST = '/v1/security/code';
const VERIFY = '/v1/security/code/verify';

type User = ReturnType<typeof userEvent.setup>;

async function openWithKey() {
  answerLikeTheApi(get, await registerFamilyOnThisPhone());
  return open();
}

async function openWithoutKey() {
  fakeRestoredSession();
  return open();
}

async function open() {
  const user = userEvent.setup();
  await render(<DeleteFamilyScreen onDeleted={onDeleted} onBack={onBack} />);
  await screen.findByRole('header', { name: 'Delete your Family' });
  return user;
}

const sendCode = () => screen.getByRole('button', { name: 'Email me a code' });
const deleteButton = () =>
  screen.getByRole('button', { name: /^(Delete my Family forever|Deleting…)$/ });
const wordField = () => screen.getByLabelText('Type DELETE to confirm');
const codeField = () => screen.getByLabelText('Code');

async function reachCodeStep(user: User) {
  await user.type(wordField(), 'DELETE');
  await user.press(sendCode());
  await screen.findByRole('header', { name: 'Check your email' });
}

async function reachVerified(user: User) {
  await reachCodeStep(user);
  await user.type(codeField(), '482917');
  await screen.findByText('Code verified');
}

beforeEach(async () => {
  await clearAll();
  for (const mock of [post, remove, get, onDeleted, onBack]) mock.mockReset();
  post.mockImplementation(async (path: string) =>
    path === VERIFY ? { proof: PROOF } : { challenge: CHALLENGE },
  );
  remove.mockResolvedValue(undefined);
});

describe('Delete Family: warning and typed word', () => {
  it('names every Child who goes with the Family and says it cannot be undone', async () => {
    await openWithKey();

    expect(await screen.findByText(/\(Aarya and Rohan\) are deleted/)).toBeOnTheScreen();
    expect(
      screen.getByText(/This cannot be undone and nothing can be recovered/),
    ).toBeOnTheScreen();
  });

  it('warns without names when the Children cannot be read, as on Start again', async () => {
    await openWithoutKey();

    expect(
      await screen.findByText(/^Your Family and every Child in it are deleted/),
    ).toBeOnTheScreen();
    expect(screen.queryByText(/Aarya/)).not.toBeOnTheScreen();
  });

  it('keeps Email me a code off until DELETE is typed exactly', async () => {
    const user = await openWithKey();
    await screen.findByText(/Aarya and Rohan/);
    expect(sendCode()).toBeDisabled();

    for (const wrong of ['delete', 'DELETE ', 'DELET', 'Delete']) {
      await user.clear(wordField());
      await user.type(wordField(), wrong);
      expect(sendCode()).toBeDisabled();
    }

    await user.clear(wordField());
    await user.type(wordField(), 'DELETE');
    expect(sendCode()).toBeEnabled();
  });

  it('asks for a code for the Family and moves on to the code', async () => {
    const user = await openWithKey();
    await screen.findByText(/Aarya and Rohan/);

    await reachCodeStep(user);

    expect(post).toHaveBeenCalledTimes(1);
    expect(post).toHaveBeenCalledWith(REQUEST, expect.anything(), { operation: 'delete-family' });
    expect(screen.getByText(/asha\.verma@gmail\.com/)).toBeOnTheScreen();
  });

  it('stays here and says so when the email could not be sent', async () => {
    post.mockRejectedValue(new ApiError('email-not-sent', 503));
    const user = await openWithKey();
    await screen.findByText(/Aarya and Rohan/);
    await user.type(wordField(), 'DELETE');

    await user.press(sendCode());

    expect(await screen.findByText('We could not send the email. Try again.')).toBeOnTheScreen();
    expect(screen.queryByRole('header', { name: 'Check your email' })).not.toBeOnTheScreen();
  });

  it('keeps the Family without any delete call', async () => {
    const user = await openWithKey();
    await screen.findByText(/Aarya and Rohan/);

    await user.press(screen.getByRole('button', { name: 'Keep my Family' }));

    expect(onBack).toHaveBeenCalledTimes(1);
    expect(post).not.toHaveBeenCalled();
    expect(remove).not.toHaveBeenCalled();
  });
});

describe('Delete Family: the emailed code', () => {
  it('keeps Delete my Family forever off until verify returns a proof', async () => {
    const user = await openWithKey();
    await screen.findByText(/Aarya and Rohan/);
    await reachCodeStep(user);
    expect(deleteButton()).toBeDisabled();

    await user.type(codeField(), '482917');

    expect(await screen.findByText('Code verified')).toBeOnTheScreen();
    expect(post).toHaveBeenLastCalledWith(VERIFY, expect.anything(), {
      challenge: CHALLENGE,
      code: '482917',
    });
    expect(deleteButton()).toBeEnabled();
    expect(screen.getByText('This is the last step. There is no undo.')).toBeOnTheScreen();
  });

  it.each([
    ['code-wrong', 400, 'That code is not right. Check it and try again.'],
    ['code-expired', 400, 'That code has expired. Email yourself a new one.'],
    ['code-attempts-used', 429, 'Too many wrong tries. Email yourself a new code.'],
  ])('refuses a %s code with a clear message and no delete', async (code, status, message) => {
    const user = await openWithKey();
    await screen.findByText(/Aarya and Rohan/);
    await reachCodeStep(user);
    post.mockRejectedValue(new ApiError(code, status));

    await user.type(codeField(), '111111');

    expect(await screen.findByText(message)).toBeOnTheScreen();
    expect(deleteButton()).toBeDisabled();
    expect(remove).not.toHaveBeenCalled();
  });

  it('deletes with exactly one call carrying the proof, wipes the phone, then tells the screen', async () => {
    await writeValue('theme', 'dark');
    const user = await openWithKey();
    await screen.findByText(/Aarya and Rohan/);
    await reachVerified(user);

    await user.press(deleteButton());

    expect(remove).toHaveBeenCalledTimes(1);
    expect(remove).toHaveBeenCalledWith('/v1/family', expect.anything(), {
      'Lantern-Code-Proof': PROOF,
    });
    expect(onDeleted).toHaveBeenCalledTimes(1);
    expect(await readValue('theme')).toBe('light');
  });

  it('ignores a second press, Cancel and Back while it deletes', async () => {
    let finish: () => void = () => undefined;
    remove.mockReturnValue(new Promise<void>((resolve) => (finish = resolve)));
    const user = await openWithKey();
    await screen.findByText(/Aarya and Rohan/);
    await reachVerified(user);

    await user.press(deleteButton());
    await user.press(deleteButton());
    await user.press(screen.getByRole('button', { name: 'Keep my Family' }));
    await act(async () => finish());

    expect(remove).toHaveBeenCalledTimes(1);
    expect(onBack).not.toHaveBeenCalled();
  });

  it('changes nothing on the phone and says so when the API cannot delete', async () => {
    remove.mockRejectedValue(new ApiError('network', null));
    await writeValue('theme', 'dark');
    const user = await openWithKey();
    await screen.findByText(/Aarya and Rohan/);
    await reachVerified(user);

    await user.press(deleteButton());

    expect(
      await screen.findByText('Could not reach Lantern. Check your connection and try again.'),
    ).toBeOnTheScreen();
    expect(onDeleted).not.toHaveBeenCalled();
    expect(await readValue('theme')).toBe('dark');
    expect(deleteButton()).toBeEnabled();
  });

  it('keeps the Family without any delete call when Keep my Family is pressed', async () => {
    const user = await openWithKey();
    await screen.findByText(/Aarya and Rohan/);
    await reachVerified(user);

    await user.press(screen.getByRole('button', { name: 'Keep my Family' }));

    expect(onBack).toHaveBeenCalledTimes(1);
    expect(remove).not.toHaveBeenCalled();
  });

  it('lets a Parent without the Recovery code finish, on the phone with no Family key', async () => {
    const user = await openWithoutKey();
    await screen.findByText(/^Your Family and every Child in it/);

    await reachVerified(user);
    await user.press(deleteButton());

    expect(remove).toHaveBeenCalledTimes(1);
    expect(onDeleted).toHaveBeenCalledTimes(1);
  });
});
