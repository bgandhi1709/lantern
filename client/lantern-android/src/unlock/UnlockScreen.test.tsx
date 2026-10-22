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

const PASSPHRASE = 'blue lantern 2026';
const WRONG = 'green lantern 2027';

const onOpened = jest.fn();
const onStartedAgain = jest.fn();
const onBack = jest.fn();

let familyKey: string;
let recoveryCode: string;
let lock: Record<string, string>;

beforeAll(async () => {
  const familyId = makeFamilyId();
  const keys = await createFamilyKeys(familyId, PASSPHRASE);
  familyKey = keys.familyKey;
  recoveryCode = keys.recoveryCode;
  lock = {
    familyId,
    passphraseWrappedKey: keys.passphraseWrappedKey,
    passphraseSalt: keys.passphraseSalt,
    recoveryWrappedKey: keys.recoveryWrappedKey,
    recoverySalt: keys.recoverySalt,
  };
});

const logs = ['log', 'info', 'warn', 'error', 'debug'].map((method) =>
  jest.spyOn(console, method as 'log').mockImplementation(() => undefined),
);

beforeEach(async () => {
  for (const mock of [get, del, onOpened, onStartedAgain, onBack]) mock.mockReset();
  logs.forEach((spy) => spy.mockClear());
  get.mockResolvedValue(lock);
  await clearAll();
  fakeRestoredSession();
});

const field = () => screen.getByTestId('unlock-field');
const openButton = () =>
  screen.getByRole('button', { name: /^(Open my Family|Opening…|Try again in)/ });

async function renderUnlock() {
  const user = userEvent.setup();
  await render(
    <UnlockScreen onOpened={onOpened} onStartedAgain={onStartedAgain} onBack={onBack} />,
  );
  await screen.findByRole('button', { name: 'Open my Family' });
  return user;
}

describe('Unlock your Family', () => {
  it('asks the API once for the locked Family when it opens, and never again while the Parent types', async () => {
    const user = await renderUnlock();

    await user.type(field(), WRONG);
    await user.press(openButton());
    await user.clear(field());
    await user.type(field(), PASSPHRASE);
    await user.press(screen.getByRole('radio', { name: 'Recovery code' }));

    expect(get).toHaveBeenCalledTimes(1);
    expect(get.mock.calls[0][0]).toBe('/v1/me');
    expect(del).not.toHaveBeenCalled();
  });

  it('opens the Family with the Passphrase and tells the screen it is done', async () => {
    const user = await renderUnlock();

    await user.type(field(), PASSPHRASE);
    await user.press(openButton());

    expect(await readFamilyKey(FAKE_UID)).toBe(familyKey);
    expect(onOpened).toHaveBeenCalledTimes(1);
  });

  it('opens the Family with the Recovery code', async () => {
    const user = await renderUnlock();

    await user.press(screen.getByRole('radio', { name: 'Recovery code' }));
    await user.type(field(), recoveryCode.toLowerCase());
    await user.press(openButton());

    expect(await readFamilyKey(FAKE_UID)).toBe(familyKey);
    expect(onOpened).toHaveBeenCalledTimes(1);
  });

  it('says a wrong value is wrong, keeps nothing, and makes the Parent wait before the next try', async () => {
    const user = await renderUnlock();

    await user.type(field(), WRONG);
    await user.press(openButton());

    expect(
      await screen.findByText('That passphrase is not right. Check it and try again.'),
    ).toBeOnTheScreen();
    expect(await screen.findByRole('button', { name: 'Try again in 5 seconds' })).toBeDisabled();
    expect(await readFamilyKey(FAKE_UID)).toBeNull();
    expect(onOpened).not.toHaveBeenCalled();
    expect(get).toHaveBeenCalledTimes(1);
  });

  it('still waits after the app is closed and opened again', async () => {
    await writeValue('unlockWait', { failures: 3, until: Date.now() + 15_000 });

    await render(
      <UnlockScreen onOpened={onOpened} onStartedAgain={onStartedAgain} onBack={onBack} />,
    );

    expect(
      await screen.findByRole('button', { name: /^Try again in 1\d seconds$/ }),
    ).toBeDisabled();
  });

  it('counts the wait down and lets the Parent try again at zero', async () => {
    jest.useFakeTimers();
    await writeValue('unlockWait', { failures: 1, until: Date.now() + 2_000 });
    await render(
      <UnlockScreen onOpened={onOpened} onStartedAgain={onStartedAgain} onBack={onBack} />,
    );
    expect(await screen.findByRole('button', { name: 'Try again in 2 seconds' })).toBeDisabled();

    await act(async () => void jest.advanceTimersByTime(2_000));

    expect(await screen.findByRole('button', { name: 'Open my Family' })).toBeDisabled();
    jest.useRealTimers();
  });

  it('clears what was typed and the error when the Parent switches to the other way in', async () => {
    const user = await renderUnlock();
    await user.type(field(), WRONG);
    await user.press(openButton());
    await screen.findByText('That passphrase is not right. Check it and try again.');

    await user.press(screen.getByRole('radio', { name: 'Recovery code' }));

    expect(field()).toHaveDisplayValue('');
    expect(screen.queryByText(/is not right/)).toBeNull();
  });

  it('shows Try again, and no field to guess at, when Lantern cannot be reached', async () => {
    get.mockRejectedValueOnce(new ApiError('unreachable', null));
    const user = userEvent.setup();
    await render(
      <UnlockScreen onOpened={onOpened} onStartedAgain={onStartedAgain} onBack={onBack} />,
    );

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
    await second.press(screen.getByRole('radio', { name: 'Recovery code' }));
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
    for (const secret of [PASSPHRASE, WRONG, recoveryCode, recoveryCode.replace(/-/g, '')]) {
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

describe('Start again', () => {
  const askToStartAgain = async () => {
    const user = await renderUnlock();
    await user.press(screen.getByRole('button', { name: 'Start again' }));
    return user;
  };

  it('explains what it does and deletes nothing until the Parent confirms', async () => {
    await askToStartAgain();

    expect(screen.getByText('This deletes your Family')).toBeOnTheScreen();
    expect(del).not.toHaveBeenCalled();
  });

  it('keeps the Family when the Parent changes their mind', async () => {
    const user = await askToStartAgain();

    await user.press(screen.getByRole('button', { name: 'Keep my Family' }));

    expect(del).not.toHaveBeenCalled();
    expect(await screen.findByRole('button', { name: 'Open my Family' })).toBeOnTheScreen();
  });

  it('deletes the Family with one call, wipes the phone, then tells the screen', async () => {
    del.mockResolvedValue(undefined);
    await writeValue('theme', 'dark');
    const user = await askToStartAgain();

    await user.press(screen.getByRole('button', { name: 'Delete my Family and start again' }));

    expect(del).toHaveBeenCalledTimes(1);
    expect(del.mock.calls[0][0]).toBe('/v1/family');
    expect(onStartedAgain).toHaveBeenCalledTimes(1);
    expect(await readValue('theme')).toBe('light');
  });

  it('ignores a second press while it deletes', async () => {
    let finish: () => void = () => undefined;
    del.mockReturnValue(new Promise<void>((resolve) => (finish = resolve)));
    const user = await askToStartAgain();
    const button = () =>
      screen.getByRole('button', { name: /^(Delete my Family and start again|Deleting…)$/ });

    await user.press(button());
    await user.press(button());
    await user.press(screen.getByRole('button', { name: 'Keep my Family' }));
    await act(async () => finish());

    expect(del).toHaveBeenCalledTimes(1);
  });

  it('says nothing was changed when the API cannot delete, and the Parent can try again', async () => {
    del.mockRejectedValueOnce(new ApiError('unreachable', null)).mockResolvedValueOnce(undefined);
    const user = await askToStartAgain();
    const button = () => screen.getByRole('button', { name: 'Delete my Family and start again' });

    await user.press(button());
    expect(await screen.findByText(/Nothing was changed/)).toBeOnTheScreen();
    expect(onStartedAgain).not.toHaveBeenCalled();

    await user.press(button());
    expect(onStartedAgain).toHaveBeenCalledTimes(1);
  });
});
