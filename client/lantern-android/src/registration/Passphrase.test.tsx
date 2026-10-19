import '@testing-library/react-native/matchers';
import { act, render, screen, userEvent } from '@testing-library/react-native';
import { BackHandler, Linking, Share, Text } from 'react-native';

import { ApiError, api } from '../shared/api';
import { readFamilyKey } from '../shared/storage';
import { AuthGate, AuthProvider } from '../auth';
import * as fakeLibrary from '../test/fakeQuickCrypto';
import { FAKE_UID, fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { secureStoreContents } from '../test/fakeSecureStore';
import { RegistrationFlow } from '.';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const get = api.get as jest.Mock;

afterEach(() => jest.restoreAllMocks());

const PASSPHRASE = 'blue lantern 2026';

const passphraseField = () => screen.getByLabelText('Passphrase');
const againField = () => screen.getByLabelText('Type it again');
const understoodBox = () =>
  screen.getByRole('checkbox', {
    name: "I understand Lantern can't reset or change it, and I will keep it safe.",
  });
const createButton = () => screen.getByRole('button', { name: 'Create passphrase' });
const savedBox = () =>
  screen.getByRole('checkbox', { name: 'I have saved my recovery code somewhere safe.' });
const continueButton = () => screen.getByRole('button', { name: 'Continue' });
const backButton = () => screen.getByRole('button', { name: 'Back' });
const codeText = () => screen.getByTestId('recovery-code').props.children as string;

const hardwareBack = () => {
  const handlers: (() => boolean | null | undefined)[] = [];
  jest.spyOn(BackHandler, 'addEventListener').mockImplementation((_event, handler) => {
    const press = () => handler({ type: 'hardwareBackPress', timeStamp: 0 });
    handlers.push(press);
    return { remove: () => void handlers.splice(handlers.indexOf(press), 1) };
  });
  return () => handlers.at(-1)?.();
};

const renderPassphraseStep = async () => {
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
  await user.press(continueButton());
  await user.press(screen.getByRole('radio', { name: /^CBSE/ }));
  await user.press(continueButton());
  return user;
};

const fillPassphrase = async (user: Awaited<ReturnType<typeof renderPassphraseStep>>) => {
  await user.type(passphraseField(), PASSPHRASE);
  await user.type(againField(), PASSPHRASE);
  await user.press(understoodBox());
};

const renderRecoveryStep = async () => {
  const user = await renderPassphraseStep();
  await fillPassphrase(user);
  await user.press(createButton());
  await screen.findByRole('header', { name: 'Save your recovery code' });
  return user;
};

describe('the Passphrase step', () => {
  it('asks for a family passphrase, explains why in calm words, and sits at step 3 of 4', async () => {
    await renderPassphraseStep();

    expect(screen.getByRole('header', { name: 'Choose a family passphrase' })).toBeOnTheScreen();
    expect(
      screen.getByText(
        "Your children's names and your details are private. This passphrase locks them on your phone before they are sent, so nobody at Lantern, and nobody who broke into our systems, could read them. It is there to protect you and your children.",
      ),
    ).toBeOnTheScreen();
    expect(screen.getByText('Please keep it safe')).toBeOnTheScreen();
    expect(
      screen.getByText(
        "Lantern never sees your passphrase, so we can't reset or change it for you. That is what keeps your family's details safe: if we could open them, someone else could too. If you lose it and your recovery code, they can't be opened again, so pick something you will remember.",
      ),
    ).toBeOnTheScreen();
    expect(screen.getByText('At least 8 characters. A few words are easier to remember.'));
    expect(screen.getByText('Step 3 of 4')).toBeOnTheScreen();
  });

  it('hides what is typed in both fields', async () => {
    await renderPassphraseStep();

    expect(passphraseField().props.secureTextEntry).toBe(true);
    expect(againField().props.secureTextEntry).toBe(true);
  });

  it('keeps Create passphrase a real disabled button until the Passphrase is long enough, typed twice and ticked', async () => {
    const user = await renderPassphraseStep();
    expect(createButton()).toBeDisabled();

    await user.type(passphraseField(), '1234567');
    await user.type(againField(), '1234567');
    await user.press(understoodBox());
    expect(createButton()).toBeDisabled();

    await user.type(passphraseField(), '8');
    expect(createButton()).toBeDisabled();
    await user.type(againField(), '8');
    expect(createButton()).toBeEnabled();

    await user.press(understoodBox());
    expect(createButton()).toBeDisabled();
  });

  it('says the two typings differ only once the second goes a different way, and gives no other error', async () => {
    const user = await renderPassphraseStep();
    expect(screen.queryByText('The two passphrases are not the same.')).not.toBeOnTheScreen();

    await user.type(passphraseField(), 'short');
    expect(screen.queryByText(/not the same|too short/)).not.toBeOnTheScreen();

    await user.type(againField(), 'shor');
    expect(screen.queryByText('The two passphrases are not the same.')).not.toBeOnTheScreen();

    await user.type(againField(), 'x');
    expect(screen.getByText('The two passphrases are not the same.')).toBeOnTheScreen();

    await user.clear(againField());
    await user.type(againField(), 'short');
    expect(screen.queryByText('The two passphrases are not the same.')).not.toBeOnTheScreen();
  });

  it('does not take spaces alone as a passphrase', async () => {
    const user = await renderPassphraseStep();
    await user.type(passphraseField(), '        ');
    await user.type(againField(), '        ');
    await user.press(understoodBox());

    expect(createButton()).toBeDisabled();
  });

  it('goes back to the Board with the pick kept', async () => {
    const user = await renderPassphraseStep();

    await user.press(backButton());

    expect(
      screen.getByRole('header', { name: 'Which board does your child follow?' }),
    ).toBeOnTheScreen();
    expect(screen.getByRole('radio', { name: /^CBSE/ })).toBeChecked();
  });

  it('shows a wait state while the key is made, and cannot be pressed twice', async () => {
    let finish: () => void = () => {};
    jest.spyOn(fakeLibrary, 'argon2').mockImplementation((_a, params, callback) => {
      finish = () => callback(null, fakeLibrary.Buffer.alloc(params.tagLength, 7));
    });
    const user = await renderPassphraseStep();
    await fillPassphrase(user);

    await user.press(createButton());

    expect(screen.getByRole('button', { name: 'Creating…' })).toBeDisabled();
    expect(
      screen.getByRole('progressbar', { name: 'Creating your passphrase…' }),
    ).toBeOnTheScreen();
    await act(async () => finish());
  });

  it('makes the keys once when the button is pressed twice, and drops them when the Parent has gone back', async () => {
    let finish: () => void = () => {};
    const derive = jest.spyOn(fakeLibrary, 'argon2').mockImplementation((_a, params, callback) => {
      finish = () => callback(null, fakeLibrary.Buffer.alloc(params.tagLength, 7));
    });
    const user = await renderPassphraseStep();
    await fillPassphrase(user);

    await user.press(createButton());
    await user.press(screen.getByRole('button', { name: 'Creating…' }));
    await user.press(backButton());

    expect(derive).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('header', { name: 'Choose a family passphrase' })).toBeOnTheScreen();
    await act(async () => finish());
  });

  it('says it could not create the passphrase, without the passphrase, and lets the Parent try again', async () => {
    jest.spyOn(fakeLibrary, 'argon2').mockImplementationOnce((_a, _params, callback) => {
      callback(new Error(`failed for ${PASSPHRASE}`), fakeLibrary.Buffer.alloc(0));
    });
    const user = await renderPassphraseStep();
    await fillPassphrase(user);

    await user.press(createButton());

    expect(await screen.findByText("Couldn't create the passphrase. Try again.")).toBeOnTheScreen();
    expect(screen.queryByText(new RegExp(PASSPHRASE))).not.toBeOnTheScreen();
    expect(createButton()).toBeEnabled();
  });
});

describe('the Recovery code step', () => {
  it('shows the code once, in groups of four, with its explanation, and no Back', async () => {
    await renderRecoveryStep();

    expect(codeText()).toMatch(/^[A-HJ-NP-Z2-9]{4}(-[A-HJ-NP-Z2-9]{4}){3}$/);
    expect(screen.getByText('RECOVERY CODE')).toBeOnTheScreen();
    expect(
      screen.getByText(
        "If you forget the passphrase, this code is the only other way to open your family's details. We show it once because we don't keep a copy: that is what keeps it private to you.",
      ),
    ).toBeOnTheScreen();
    expect(screen.getByText('Step 3 of 4')).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Back' })).not.toBeOnTheScreen();
  });

  it('keeps Continue a real disabled button until the Parent ticks that they saved the code', async () => {
    const user = await renderRecoveryStep();
    expect(continueButton()).toBeDisabled();

    await user.press(savedBox());
    expect(continueButton()).toBeEnabled();

    await user.press(savedBox());
    expect(continueButton()).toBeDisabled();
  });

  it("ignores Android's back button, so the code cannot be left behind by accident", async () => {
    const back = hardwareBack();
    await renderRecoveryStep();

    expect(await act(async () => back())).toBe(true);

    expect(screen.getByRole('header', { name: 'Save your recovery code' })).toBeOnTheScreen();
  });

  it("opens the phone's mail app with a message to the Google address, and sends nothing to the API", async () => {
    const open = jest.spyOn(Linking, 'openURL').mockResolvedValue(true);
    const user = await renderRecoveryStep();

    await user.press(screen.getByRole('button', { name: 'Email it to myself' }));

    const [url] = open.mock.calls[0];
    const mail = new URL(url);
    expect(mail.protocol).toBe('mailto:');
    expect(decodeURIComponent(mail.pathname)).toBe('asha.verma@gmail.com');
    expect(mail.searchParams.get('subject')).toBe('Your Lantern recovery code');
    expect(mail.searchParams.get('body')).toContain(codeText());
    expect(api.post).not.toHaveBeenCalled();
    expect(api.put).not.toHaveBeenCalled();
  });

  it('opens the share sheet from Save elsewhere, and as the fallback when there is no mail app', async () => {
    const share = jest.spyOn(Share, 'share').mockResolvedValue({ action: 'sharedAction' });
    jest.spyOn(Linking, 'openURL').mockRejectedValue(new Error('no mail app'));
    const user = await renderRecoveryStep();

    await user.press(screen.getByRole('button', { name: 'Save elsewhere' }));
    expect(share).toHaveBeenCalledTimes(1);
    expect(share.mock.calls[0][0].message).toContain(codeText());

    await user.press(screen.getByRole('button', { name: 'Email it to myself' }));
    expect(share).toHaveBeenCalledTimes(2);
  });

  it('says so when neither the mail app nor the share sheet opens', async () => {
    jest.spyOn(Share, 'share').mockRejectedValue(new Error('no share targets'));
    jest.spyOn(Linking, 'openURL').mockRejectedValue(new Error('no mail app'));
    const user = await renderRecoveryStep();

    await user.press(screen.getByRole('button', { name: 'Save elsewhere' }));

    expect(
      screen.getByText("Couldn't open that. Try the other button, or write the code down."),
    ).toBeOnTheScreen();
  });

  it('goes on to step 4 once ticked, and Back there returns to the same code', async () => {
    const user = await renderRecoveryStep();
    const code = codeText();
    await user.press(savedBox());

    await user.press(continueButton());

    expect(screen.getByText('Step 4 of 4')).toBeOnTheScreen();
    expect(screen.getByRole('header', { name: 'Add your first child' })).toBeOnTheScreen();

    await user.press(backButton());

    expect(screen.getByRole('header', { name: 'Save your recovery code' })).toBeOnTheScreen();
    expect(codeText()).toBe(code);
  });
});

describe('what the Passphrase and Recovery code leave behind', () => {
  it('never reaches the API, storage, a log line or the screen after it is made', async () => {
    const logs = [
      jest.spyOn(console, 'log'),
      jest.spyOn(console, 'warn'),
      jest.spyOn(console, 'error'),
    ];
    const user = await renderRecoveryStep();
    const code = codeText();
    await user.press(savedBox());
    await user.press(continueButton());

    expect(api.post).not.toHaveBeenCalled();
    expect(JSON.stringify(secureStoreContents())).not.toContain(code);
    expect(JSON.stringify(secureStoreContents())).not.toContain(PASSPHRASE);
    expect(await readFamilyKey(FAKE_UID)).toBeNull();
    expect(secureStoreContents()).toEqual({});
    for (const log of logs) expect(log).not.toHaveBeenCalled();
    expect(screen.queryByText(new RegExp(`${PASSPHRASE}|${code}`))).not.toBeOnTheScreen();
  });
});
