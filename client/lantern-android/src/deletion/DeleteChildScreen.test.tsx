import '@testing-library/react-native/matchers';
import { act, render, screen, userEvent } from '@testing-library/react-native';

import { ApiError, api } from '../shared/api';
import { clearAll, readValue, writeValue } from '../shared/storage';
import { AARYA, ROHAN, answerLikeTheApi, registerFamilyOnThisPhone } from '../test/fakeFamily';
import { DeleteChildScreen } from './DeleteChildScreen';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const get = api.get as jest.Mock;
const post = api.post as jest.Mock;
const remove = api.delete as jest.Mock;

const onDeleted = jest.fn();
const onBack = jest.fn();
const onDeleteFamily = jest.fn();

const PROOF = 'proof-for-aarya';
const REQUEST = '/v1/family/delete-codes';
const VERIFY = '/v1/family/delete-codes/verify';

async function openScreen() {
  const familyKey = await registerFamilyOnThisPhone();
  answerLikeTheApi(get, familyKey);
  const user = userEvent.setup();
  await render(
    <DeleteChildScreen onDeleted={onDeleted} onBack={onBack} onDeleteFamily={onDeleteFamily} />,
  );
  await screen.findByRole('header', { name: 'Delete a child' });
  return { user, familyKey };
}

const child = (name: string) => screen.getByRole('radio', { name: new RegExp(`^${name},`) });
const sendCode = () => screen.getByRole('button', { name: 'Email me a code' });
const deleteButton = () => screen.getByRole('button', { name: /^(Delete child|Deleting…)$/ });
const nameField = () => screen.getByLabelText(/^Type \w+ to confirm$/);
const codeField = () => screen.getByLabelText('Code');

async function pickAarya(user: ReturnType<typeof userEvent.setup>) {
  await user.press(await screen.findByRole('radio', { name: /^Aarya,/ }));
}

async function reachCodeStep(user: ReturnType<typeof userEvent.setup>) {
  await pickAarya(user);
  await user.type(nameField(), 'Aarya');
  await user.press(sendCode());
  await screen.findByRole('header', { name: 'Check your email' });
}

beforeEach(async () => {
  await clearAll();
  [post, remove, onDeleted, onBack, onDeleteFamily].forEach((mock) => mock.mockReset());
  post.mockImplementation(async (path: string) => (path === VERIFY ? { proof: PROOF } : undefined));
  remove.mockResolvedValue(undefined);
});

describe('Delete a child: choose and confirm', () => {
  it('lists the Children with none picked and nothing to send yet', async () => {
    await openScreen();

    expect(child('Aarya')).not.toBeChecked();
    expect(child('Rohan')).not.toBeChecked();
    expect(screen.queryByText(/erases .* data for good/)).not.toBeOnTheScreen();
    expect(sendCode()).toBeDisabled();
  });

  it('warns in the name of the Child picked and asks for that name', async () => {
    const { user } = await openScreen();

    await pickAarya(user);

    expect(child('Aarya')).toBeChecked();
    expect(screen.getByText('This erases Aarya’s data for good')).toBeOnTheScreen();
    expect(
      screen.getByText(/Aarya’s Plans, Checks, marks and progress are deleted/),
    ).toBeOnTheScreen();
    expect(screen.getByText('Type Aarya to confirm')).toBeOnTheScreen();
    expect(sendCode()).toBeDisabled();
  });

  it('turns Email me a code on only when the name is typed exactly', async () => {
    const { user } = await openScreen();
    await pickAarya(user);

    await user.type(nameField(), 'aarya');
    expect(sendCode()).toBeDisabled();

    await user.clear(nameField());
    await user.type(nameField(), 'Aarya');
    expect(sendCode()).toBeEnabled();
  });

  it('forgets the typed name when another Child is picked', async () => {
    const { user } = await openScreen();
    await pickAarya(user);
    await user.type(nameField(), 'Aarya');

    await user.press(child('Rohan'));

    expect(nameField()).toHaveDisplayValue('');
    expect(sendCode()).toBeDisabled();
  });

  it('asks for a code for that Child and moves on to the code', async () => {
    const { user } = await openScreen();
    await pickAarya(user);
    await user.type(nameField(), 'Aarya');

    await user.press(sendCode());

    expect(post).toHaveBeenCalledTimes(1);
    expect(post).toHaveBeenCalledWith(REQUEST, expect.anything(), {
      scope: 'child',
      childId: AARYA,
    });
    expect(await screen.findByRole('header', { name: 'Check your email' })).toBeOnTheScreen();
    expect(screen.getByText(/asha\.verma@gmail\.com/)).toBeOnTheScreen();
  });

  it('stays here and says so when the email could not be sent', async () => {
    post.mockRejectedValue(new ApiError('email-not-sent', 503));
    const { user } = await openScreen();
    await pickAarya(user);
    await user.type(nameField(), 'Aarya');

    await user.press(sendCode());

    expect(await screen.findByText('We could not send the email. Try again.')).toBeOnTheScreen();
    expect(screen.queryByRole('header', { name: 'Check your email' })).not.toBeOnTheScreen();
  });

  it.each([
    ['delete-code-too-soon', 429, 'Please wait a minute before asking for another code.'],
    [
      'email-not-verified',
      409,
      'Your Google email is not verified, so Lantern cannot send a code.',
    ],
    ['network', null, 'Could not reach Lantern. Check your connection and try again.'],
    ['http-500', 500, 'Something went wrong. Nothing was deleted. Try again.'],
  ])('says what went wrong when asking for a code fails with %s', async (code, status, message) => {
    post.mockRejectedValue(new ApiError(code, status));
    const { user } = await openScreen();
    await pickAarya(user);
    await user.type(nameField(), 'Aarya');

    await user.press(sendCode());

    expect(await screen.findByText(message)).toBeOnTheScreen();
  });

  it('cancels without calling the API', async () => {
    const { user } = await openScreen();

    await user.press(screen.getByRole('button', { name: 'Cancel' }));

    expect(onBack).toHaveBeenCalledTimes(1);
    expect(post).not.toHaveBeenCalled();
    expect(remove).not.toHaveBeenCalled();
  });

  it('says so when the Family cannot be loaded', async () => {
    await registerFamilyOnThisPhone();
    get.mockRejectedValue(new ApiError('network', null));
    await render(
      <DeleteChildScreen onDeleted={onDeleted} onBack={onBack} onDeleteFamily={onDeleteFamily} />,
    );

    expect(await screen.findByText('Could not load your Family. Try again.')).toBeOnTheScreen();
    expect(screen.queryByRole('radio')).not.toBeOnTheScreen();
  });

  it('does not offer the only Child for deleting, and points to Delete Family', async () => {
    const familyKey = await registerFamilyOnThisPhone();
    answerLikeTheApi(get, familyKey);
    const everyone = get.getMockImplementation() as (
      path: string,
      schema: never,
    ) => Promise<{
      children: unknown[];
    }>;
    get.mockImplementation(async (path: string, schema: never) => {
      const family = await everyone(path, schema);
      return path === '/v1/me' ? { ...family, children: family.children.slice(0, 1) } : family;
    });
    const user = userEvent.setup();
    await render(
      <DeleteChildScreen onDeleted={onDeleted} onBack={onBack} onDeleteFamily={onDeleteFamily} />,
    );

    expect(await screen.findByText('Your Family needs at least one child')).toBeOnTheScreen();
    expect(screen.queryByRole('radio')).not.toBeOnTheScreen();

    await user.press(screen.getByRole('button', { name: 'Delete Family' }));

    expect(onDeleteFamily).toHaveBeenCalledTimes(1);
    expect(post).not.toHaveBeenCalled();
  });
});

describe('Delete a child: the emailed code', () => {
  it('keeps Delete child off until the code is complete and right', async () => {
    const { user } = await openScreen();
    await reachCodeStep(user);

    await user.type(codeField(), '4829');
    expect(deleteButton()).toBeDisabled();
    expect(post).toHaveBeenCalledTimes(1);

    await user.type(codeField(), '17');

    expect(post).toHaveBeenLastCalledWith(VERIFY, expect.anything(), { code: '482917' });
    expect(await screen.findByText('Code verified')).toBeOnTheScreen();
    expect(deleteButton()).toBeEnabled();
  });

  it('takes digits only and no more than six', async () => {
    const { user } = await openScreen();
    await reachCodeStep(user);

    await user.type(codeField(), '48a2 9b17999');

    expect(codeField()).toHaveDisplayValue('482917');
  });

  it.each([
    ['delete-code-wrong', 400, 'That code is not right. Check it and try again.'],
    ['delete-code-expired', 400, 'That code has expired. Email yourself a new one.'],
    ['delete-code-attempts-used', 429, 'Too many wrong tries. Email yourself a new code.'],
    ['network', null, 'Could not reach Lantern. Check your connection and try again.'],
  ])(
    'refuses a code the API rejects with %s and keeps Delete child off',
    async (code, status, message) => {
      const { user } = await openScreen();
      await reachCodeStep(user);
      post.mockRejectedValue(new ApiError(code, status));

      await user.type(codeField(), '111111');

      expect(await screen.findByText(message)).toBeOnTheScreen();
      expect(deleteButton()).toBeDisabled();
      expect(remove).not.toHaveBeenCalled();
    },
  );

  it('forgets a rejected code as soon as the Parent edits it', async () => {
    const { user } = await openScreen();
    await reachCodeStep(user);
    post.mockRejectedValueOnce(new ApiError('delete-code-wrong', 400));
    await user.type(codeField(), '111111');
    await screen.findByText('That code is not right. Check it and try again.');

    await user.clear(codeField());

    expect(
      screen.queryByText('That code is not right. Check it and try again.'),
    ).not.toBeOnTheScreen();
  });

  it('deletes the Child with the proof, makes the other Child the active one and leaves', async () => {
    await writeValue('activeChildId', AARYA);
    const { user } = await openScreen();
    await reachCodeStep(user);
    await user.type(codeField(), '482917');
    await screen.findByText('Code verified');

    await user.press(deleteButton());

    expect(remove).toHaveBeenCalledTimes(1);
    expect(remove).toHaveBeenCalledWith(`/v1/family/children/${AARYA}`, expect.anything(), {
      'Lantern-Delete-Proof': PROOF,
    });
    expect(await readValue('activeChildId')).toBe(ROHAN);
    expect(onDeleted).toHaveBeenCalledTimes(1);
  });

  it('ignores a second press while the delete is running', async () => {
    let finish: () => void = () => undefined;
    remove.mockReturnValue(new Promise<void>((resolve) => (finish = resolve)));
    const { user } = await openScreen();
    await reachCodeStep(user);
    await user.type(codeField(), '482917');
    await screen.findByText('Code verified');

    await user.press(deleteButton());
    await user.press(deleteButton());
    await act(async () => finish());

    expect(remove).toHaveBeenCalledTimes(1);
  });

  it('says nothing was deleted when the delete fails, and lets the Parent try again', async () => {
    remove.mockRejectedValueOnce(new ApiError('http-500', 500));
    const { user } = await openScreen();
    await reachCodeStep(user);
    await user.type(codeField(), '482917');
    await screen.findByText('Code verified');

    await user.press(deleteButton());

    expect(
      await screen.findByText('Something went wrong. Nothing was deleted. Try again.'),
    ).toBeOnTheScreen();
    expect(onDeleted).not.toHaveBeenCalled();

    await user.press(deleteButton());

    expect(remove).toHaveBeenCalledTimes(2);
    expect(onDeleted).toHaveBeenCalledTimes(1);
  });

  it('asks for a new code and clears the old one', async () => {
    const { user } = await openScreen();
    await reachCodeStep(user);
    await user.type(codeField(), '482917');
    await screen.findByText('Code verified');

    await user.press(screen.getByRole('button', { name: 'Email me a new code' }));

    expect(post).toHaveBeenLastCalledWith(REQUEST, expect.anything(), {
      scope: 'child',
      childId: AARYA,
    });
    expect(await screen.findByText('A new code is on its way')).toBeOnTheScreen();
    expect(codeField()).toHaveDisplayValue('');
    expect(deleteButton()).toBeDisabled();
  });

  it('says to wait when a new code is asked for too soon', async () => {
    const { user } = await openScreen();
    await reachCodeStep(user);
    post.mockRejectedValue(new ApiError('delete-code-too-soon', 429));

    await user.press(screen.getByRole('button', { name: 'Email me a new code' }));

    expect(
      await screen.findByText('Please wait a minute before asking for another code.'),
    ).toBeOnTheScreen();
  });

  it('cancels from the code step without deleting', async () => {
    const { user } = await openScreen();
    await reachCodeStep(user);

    await user.press(screen.getByRole('button', { name: 'Cancel' }));

    expect(onBack).toHaveBeenCalledTimes(1);
    expect(remove).not.toHaveBeenCalled();
  });

  it('goes back from the code step to the name without leaving the screen', async () => {
    const { user } = await openScreen();
    await reachCodeStep(user);

    await user.press(screen.getByRole('button', { name: 'Back' }));

    expect(await screen.findByRole('header', { name: 'Delete a child' })).toBeOnTheScreen();
    expect(onBack).not.toHaveBeenCalled();
  });
});
