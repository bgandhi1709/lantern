import '@testing-library/react-native/matchers';
import { act, render, screen, userEvent, waitFor } from '@testing-library/react-native';

import { ApiError, api } from '../shared/api';
import { createFamilyKeys, makeFamilyId } from '../shared/crypto';
import { clearAll, readFamilyKey, readValue, writeValue } from '../shared/storage';
import { FAKE_UID, fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { secureStoreContents } from '../test/fakeSecureStore';
import { UnlockScreen } from '.';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const get = api.get as jest.Mock;
const del = api.delete as jest.Mock;

const WRONG = 'ABCD-EFGH-JKLM-NPQR';

const onOpened = jest.fn();
const onBack = jest.fn();

let familyKey: string;
let recoveryCode: string;
let lock: Record<string, string>;

beforeAll(async () => {
  const familyId = makeFamilyId();
  const keys = await createFamilyKeys(familyId);
  familyKey = keys.familyKey;
  recoveryCode = keys.recoveryCode;
  lock = {
    familyId,
    recoveryWrappedKey: keys.recoveryWrappedKey,
    recoverySalt: keys.recoverySalt,
  };
});

const logs = ['log', 'info', 'warn', 'error', 'debug'].map((method) =>
  jest.spyOn(console, method as 'log').mockImplementation(() => undefined),
);

beforeEach(async () => {
  for (const mock of [get, del, onOpened, onBack]) mock.mockReset();
  logs.forEach((spy) => spy.mockClear());
  get.mockResolvedValue(lock);
  await clearAll();
  fakeRestoredSession();
});

const field = () => screen.getByTestId('unlock-field');
const openButton = () =>
  screen.getByRole('button', { name: /^(Open my Family|Opening…|Try again in)/ });
const WRONG_TEXT = 'That recovery code is not right. Check it and try again.';

async function renderUnlock() {
  const user = userEvent.setup();
  await render(<UnlockScreen onOpened={onOpened} onBack={onBack} />);
  await screen.findByRole('button', { name: 'Open my Family' });
  return user;
}

describe('Unlock your Family', () => {
  it('asks only for the Recovery code, and says where it was emailed', async () => {
    await renderUnlock();

    expect(screen.getByText('Recovery code')).toBeOnTheScreen();
    expect(screen.queryByText(/assphrase/)).not.toBeOnTheScreen();
    expect(screen.queryByRole('radio')).not.toBeOnTheScreen();
    expect(screen.getByText(/look for "Your Lantern recovery code"/)).toBeOnTheScreen();
  });

  it('offers no Start again and no Sign out, so a Parent in a hurry is not tempted', async () => {
    await renderUnlock();

    expect(screen.queryByRole('button', { name: 'Start again' })).not.toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Sign out' })).not.toBeOnTheScreen();
    expect(screen.queryByText(/Lost both/)).not.toBeOnTheScreen();
  });

  it('asks the API once for the locked Family when it opens, and never again while the Parent types', async () => {
    const user = await renderUnlock();

    await user.type(field(), WRONG);
    await user.press(openButton());
    await user.clear(field());
    await user.type(field(), recoveryCode);

    expect(get).toHaveBeenCalledTimes(1);
    expect(get.mock.calls[0][0]).toBe('/v1/me');
    expect(del).not.toHaveBeenCalled();
  });

  it('opens the Family with the Recovery code however it is typed and tells the screen it is done', async () => {
    const user = await renderUnlock();

    await user.type(field(), recoveryCode.toLowerCase().replace(/-/g, ' '));
    await user.press(openButton());

    expect(await readFamilyKey(FAKE_UID)).toBe(familyKey);
    expect(onOpened).toHaveBeenCalledTimes(1);
  });

  it('says a wrong code is wrong, keeps nothing, and makes the Parent wait before the next try', async () => {
    const user = await renderUnlock();

    await user.type(field(), WRONG);
    await user.press(openButton());

    expect(await screen.findByText(WRONG_TEXT)).toBeOnTheScreen();
    expect(await screen.findByRole('button', { name: 'Try again in 5 seconds' })).toBeDisabled();
    expect(await readFamilyKey(FAKE_UID)).toBeNull();
    expect(onOpened).not.toHaveBeenCalled();
    expect(get).toHaveBeenCalledTimes(1);
  });

  it('clears the error as soon as the Parent edits the code', async () => {
    const user = await renderUnlock();
    await user.type(field(), WRONG);
    await user.press(openButton());
    await screen.findByText(WRONG_TEXT);

    await user.type(field(), 'X');

    expect(screen.queryByText(WRONG_TEXT)).toBeNull();
  });

  it('still waits after the app is closed and opened again', async () => {
    await writeValue('unlockWait', { failures: 3, until: Date.now() + 15_000 });

    await render(<UnlockScreen onOpened={onOpened} onBack={onBack} />);

    expect(
      await screen.findByRole('button', { name: /^Try again in 1\d seconds$/ }),
    ).toBeDisabled();
  });

  it('counts the wait down and lets the Parent try again at zero', async () => {
    jest.useFakeTimers();
    await writeValue('unlockWait', { failures: 1, until: Date.now() + 2_000 });
    await render(<UnlockScreen onOpened={onOpened} onBack={onBack} />);
    expect(await screen.findByRole('button', { name: 'Try again in 2 seconds' })).toBeDisabled();

    await act(async () => void jest.advanceTimersByTime(2_000));

    expect(await screen.findByRole('button', { name: 'Open my Family' })).toBeDisabled();
    jest.useRealTimers();
  });

  it('shows Try again, and no way to guess, when Lantern cannot be reached', async () => {
    get.mockRejectedValueOnce(new ApiError('unreachable', null));
    const user = userEvent.setup();
    await render(<UnlockScreen onOpened={onOpened} onBack={onBack} />);

    expect(await screen.findByText(/Can't reach Lantern/)).toBeOnTheScreen();
    await user.press(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByRole('button', { name: 'Open my Family' })).toBeDisabled();
    expect(get).toHaveBeenCalledTimes(2);
  });

  it('goes back without changing anything', async () => {
    const user = await renderUnlock();

    await user.press(screen.getByRole('button', { name: 'Back' }));

    expect(onBack).toHaveBeenCalledTimes(1);
    expect(await readFamilyKey(FAKE_UID)).toBeNull();
  });
});

describe('what the Unlock screen keeps to itself', () => {
  it('never stores, logs or sends what the Parent types, only the Family key', async () => {
    const first = await renderUnlock();
    await first.type(field(), WRONG);
    await first.press(openButton());
    await screen.findByRole('button', { name: 'Try again in 5 seconds' });
    await screen.unmount();
    await writeValue('unlockWait', { failures: 0, until: 0 });
    const second = await renderUnlock();
    await second.type(field(), recoveryCode);
    await second.press(openButton());
    await waitFor(() => expect(onOpened).toHaveBeenCalled());

    const everything = JSON.stringify([
      secureStoreContents(),
      await readValue('unlockWait'),
      logs.flatMap((spy) => spy.mock.calls),
      get.mock.calls.map((call) => call[0]),
      del.mock.calls.map((call) => call[0]),
    ]);
    for (const secret of [WRONG, recoveryCode, recoveryCode.replace(/-/g, '')]) {
      expect(everything).not.toContain(secret);
    }
    expect(await readFamilyKey(FAKE_UID)).toBe(familyKey);
  });

  it('hides what is typed as it is typed', async () => {
    await renderUnlock();

    expect(field().props.secureTextEntry).toBe(true);
    expect(field().props.autoCorrect).toBe(false);
  });
});
