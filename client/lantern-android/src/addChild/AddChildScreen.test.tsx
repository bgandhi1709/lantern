import '@testing-library/react-native/matchers';
import { act, fireEvent, render, screen, userEvent } from '@testing-library/react-native';
import { BackHandler } from 'react-native';

import { ApiError, api } from '../shared/api';
import { createFamilyKeys, makeFamilyId, unlockText } from '../shared/crypto';
import { clearAll, keepFamilyKey, readValue } from '../shared/storage';
import { registerFamilyOnThisPhone } from '../test/fakeFamily';
import { fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { AddChildScreen } from './AddChildScreen';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const post = api.post as jest.Mock;
const THIS_YEAR = new Date().getFullYear();
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;

const onAdded = jest.fn();
const onBack = jest.fn();

const addButton = () => screen.getByRole('button', { name: /^(Add child|Adding…|Try again)$/ });

const openScreen = async (board: 'cbse' | 'ssc' = 'cbse') => {
  const familyKey = await registerFamilyOnThisPhone();
  const user = userEvent.setup();
  await render(<AddChildScreen board={board} onAdded={onAdded} onBack={onBack} />);
  return { user, familyKey };
};

const openScreenWithoutTheKey = async (keepOtherAccountsKey: boolean) => {
  fakeRestoredSession();
  if (keepOtherAccountsKey) {
    const { familyKey } = await createFamilyKeys(makeFamilyId());
    await keepFamilyKey('someone-elses-uid', familyKey);
  }
  const user = userEvent.setup();
  await render(<AddChildScreen board="cbse" onAdded={onAdded} onBack={onBack} />);
  return { user };
};

const fillChild = async (user: ReturnType<typeof userEvent.setup>, name = 'Mira') => {
  await user.type(screen.getByLabelText("Child's name"), name);
  await user.press(screen.getByRole('radio', { name: 'Class 4' }));
  await fireEvent(screen.getByLabelText('Birth year'), 'valueChange', String(THIS_YEAR - 9));
};

beforeEach(async () => {
  await clearAll();
  post.mockReset().mockResolvedValue({});
  onAdded.mockReset();
  onBack.mockReset();
});

describe('Add a child', () => {
  it('says how many Children a Family can have and keeps Add child disabled until the form is complete', async () => {
    const { user } = await openScreen();

    expect(screen.getByRole('header', { name: 'Add a child' })).toBeOnTheScreen();
    expect(screen.getByText('A Family can have up to 6 Children.')).toBeOnTheScreen();
    expect(addButton()).toBeDisabled();

    await user.type(screen.getByLabelText("Child's name"), 'Mira');
    await user.press(screen.getByRole('radio', { name: 'Class 4' }));
    expect(addButton()).toBeDisabled();

    await fireEvent(screen.getByLabelText('Birth year'), 'valueChange', String(THIS_YEAR - 9));
    expect(addButton()).toBeEnabled();
  });

  it('offers Classes 1 to 5 on SSC', async () => {
    await openScreen('ssc');
    expect(screen.getAllByRole('radio', { name: /^Class \d+$/ })).toHaveLength(5);
  });

  it('does not take a name of spaces alone', async () => {
    const { user } = await openScreen();
    await fillChild(user, '   ');

    expect(addButton()).toBeDisabled();
  });

  it('sends the Child with a new id and its personal details locked, makes it the active Child and leaves', async () => {
    const { user, familyKey } = await openScreen();
    await fillChild(user);
    await user.type(screen.getByLabelText('School (optional)'), 'Sunrise School');
    await user.press(addButton());

    const [path, , body] = post.mock.calls[0];
    expect(path).toBe('/v1/family/children');
    expect(body.childId).toMatch(UUID);
    expect(body.classLevel).toBe(4);
    expect(unlockText(familyKey, body.name)).toBe('Mira');
    expect(unlockText(familyKey, body.birthYear)).toBe(String(THIS_YEAR - 9));
    expect(unlockText(familyKey, body.school)).toBe('Sunrise School');
    expect(JSON.stringify(body)).not.toContain('Mira');
    expect(await readValue('activeChildId')).toBe(body.childId);
    expect(onAdded).toHaveBeenCalledTimes(1);
  });

  it('shows the limit message with Try again when the API says the Family is full, and tries again with the same id', async () => {
    const { user } = await openScreen();
    await fillChild(user);
    post.mockRejectedValueOnce(new ApiError('child-limit-reached', 409));
    await user.press(addButton());

    expect(
      await screen.findByText('This family already has 6 children, so another cannot be added.'),
    ).toBeOnTheScreen();
    expect(addButton()).toHaveTextContent('Try again');
    expect(onAdded).not.toHaveBeenCalled();

    await user.press(addButton());

    expect(post).toHaveBeenCalledTimes(2);
    expect(post.mock.calls[1][2].childId).toBe(post.mock.calls[0][2].childId);
    expect(onAdded).toHaveBeenCalledTimes(1);
  });

  it('says the connection failed, and any edit clears the message', async () => {
    const { user } = await openScreen();
    await fillChild(user);
    post.mockRejectedValueOnce(new ApiError('network', 0));
    await user.press(addButton());

    expect(
      await screen.findByText("Couldn't reach Lantern. Check your connection, then try again."),
    ).toBeOnTheScreen();

    await user.type(screen.getByLabelText('School (optional)'), 'S');

    expect(
      screen.queryByText("Couldn't reach Lantern. Check your connection, then try again."),
    ).not.toBeOnTheScreen();
    expect(addButton()).toHaveTextContent('Add child');
  });

  it('says why when the Class is not available, and for any other failure', async () => {
    const { user } = await openScreen();
    await fillChild(user);
    post.mockRejectedValueOnce(new ApiError('class-not-available', 400));
    await user.press(addButton());
    expect(await screen.findByText(/doesn't have books for this Class/)).toBeOnTheScreen();

    post.mockRejectedValueOnce(new ApiError('boom', 500));
    await user.press(addButton());
    expect(
      await screen.findByText(
        'Something went wrong, and the child was not added. Please try again.',
      ),
    ).toBeOnTheScreen();
  });

  it('ignores a second press and Back while it saves', async () => {
    const { user } = await openScreen();
    await fillChild(user);
    let finish: (value: unknown) => void = () => undefined;
    post.mockReturnValue(new Promise((resolve) => (finish = resolve)));
    await user.press(addButton());
    await user.press(addButton());

    expect(post).toHaveBeenCalledTimes(1);
    expect(addButton()).toBeDisabled();
    await user.press(screen.getByRole('button', { name: 'Back' }));
    expect(onBack).not.toHaveBeenCalled();

    await act(async () => finish({}));
    expect(onAdded).toHaveBeenCalledTimes(1);
  });

  it.each([
    ['this phone has no Family key', false],
    ["this phone only has another account's Family key", true],
  ])('sends nothing and stores nothing when %s', async (_case, otherAccountsKey) => {
    const { user } = await openScreenWithoutTheKey(otherAccountsKey);
    await fillChild(user);
    await user.press(addButton());

    expect(
      await screen.findByText(
        'Something went wrong, and the child was not added. Please try again.',
      ),
    ).toBeOnTheScreen();
    expect(post).not.toHaveBeenCalled();
    expect(await readValue('activeChildId')).toBeNull();
    expect(onAdded).not.toHaveBeenCalled();
  });

  it('goes back from the arrow and from the Android Back button', async () => {
    const handlers: (() => boolean | null | undefined)[] = [];
    jest.spyOn(BackHandler, 'addEventListener').mockImplementation((_event, handler) => {
      const press = () => handler({ type: 'hardwareBackPress', timeStamp: 0 });
      handlers.push(press);
      return { remove: () => void handlers.splice(handlers.indexOf(press), 1) };
    });
    const { user } = await openScreen();

    await user.press(screen.getByRole('button', { name: 'Back' }));
    handlers.at(-1)?.();

    expect(onBack).toHaveBeenCalledTimes(2);
  });
});
