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

const savedBox = () => screen.getByRole('checkbox', { name: 'I have sent the email to myself.' });
const emailTheCode = async (user: ReturnType<typeof userEvent.setup>) => {
  jest.spyOn(Linking, 'openURL').mockResolvedValue(true);
  await user.press(screen.getByRole('button', { name: 'Email it to myself' }));
};
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

const renderRecoveryStep = async () => {
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
  await screen.findByRole('header', { name: 'Save your recovery code' });
  await screen.findByTestId('recovery-code');
  return user;
};

describe('the Recovery code step', () => {
  it('makes the code when the step opens, with a wait state, and only once', async () => {
    let finish: () => void = () => {};
    const derive = jest.spyOn(fakeLibrary, 'argon2').mockImplementation((_a, params, callback) => {
      finish = () => callback(null, fakeLibrary.Buffer.alloc(params.tagLength, 7));
    });
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

    expect(
      await screen.findByRole('progressbar', { name: 'Making your recovery code…' }),
    ).toBeOnTheScreen();
    expect(continueButton()).toBeDisabled();
    await act(async () => finish());
    expect(derive).toHaveBeenCalledTimes(1);
  });

  it('says it could not make the code, and lets the Parent try again', async () => {
    jest.spyOn(fakeLibrary, 'argon2').mockImplementationOnce((_a, _params, callback) => {
      callback(new Error('failed'), fakeLibrary.Buffer.alloc(0));
    });
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

    expect(
      await screen.findByText("Couldn't make your recovery code. Try again."),
    ).toBeOnTheScreen();
    await user.press(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByTestId('recovery-code')).toBeOnTheScreen();
  });

  it('shows the code in groups of four, with its explanation', async () => {
    await renderRecoveryStep();

    expect(codeText()).toMatch(/^[A-HJ-NP-Z2-9]{4}(-[A-HJ-NP-Z2-9]{4}){3}$/);
    expect(screen.getByText('RECOVERY CODE')).toBeOnTheScreen();
    expect(
      screen.getByText(
        "This code is the only way to open your family's details on a new phone. Lantern never sees it and can't reset it: that is what keeps your family's details private. Email it to yourself now, so it is always in your inbox.",
      ),
    ).toBeOnTheScreen();
    expect(screen.getByText('Step 3 of 4')).toBeOnTheScreen();
  });

  it('keeps Continue a real disabled button until the Parent has opened the email and ticked that they sent it', async () => {
    const user = await renderRecoveryStep();
    expect(continueButton()).toBeDisabled();
    expect(
      screen.getByText('Send the email to yourself first. Then the code is always in your inbox.'),
    ).toBeOnTheScreen();

    await user.press(savedBox());
    expect(continueButton()).toBeDisabled();

    await emailTheCode(user);
    expect(continueButton()).toBeEnabled();
    expect(screen.queryByText(/Send the email to yourself first/)).not.toBeOnTheScreen();

    await user.press(savedBox());
    expect(continueButton()).toBeDisabled();
  });

  it('does not count the email as opened when neither the mail app nor the share sheet opens', async () => {
    jest.spyOn(Share, 'share').mockRejectedValue(new Error('no share targets'));
    jest.spyOn(Linking, 'openURL').mockRejectedValue(new Error('no mail app'));
    const user = await renderRecoveryStep();
    await user.press(savedBox());

    await user.press(screen.getByRole('button', { name: 'Email it to myself' }));

    expect(continueButton()).toBeDisabled();
  });

  it('goes back to the Board with Back or Android back, and shows the same code when it returns', async () => {
    const back = hardwareBack();
    const user = await renderRecoveryStep();
    const code = codeText();

    expect(await act(async () => back())).toBe(true);
    expect(
      screen.getByRole('header', { name: 'Which board does your child follow?' }),
    ).toBeOnTheScreen();
    await user.press(continueButton());

    expect(await screen.findByTestId('recovery-code')).toBeOnTheScreen();
    expect(codeText()).toBe(code);
  });

  it("opens the phone's mail app with a message to the Google address, and sends nothing to the API", async () => {
    const open = jest.spyOn(Linking, 'openURL').mockClear().mockResolvedValue(true);
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
    await emailTheCode(user);
    await user.press(savedBox());

    await user.press(continueButton());

    expect(screen.getByText('Step 4 of 4')).toBeOnTheScreen();
    expect(screen.getByRole('header', { name: 'Add your first child' })).toBeOnTheScreen();

    await user.press(backButton());

    expect(screen.getByRole('header', { name: 'Save your recovery code' })).toBeOnTheScreen();
    expect(codeText()).toBe(code);
  });
});

describe('what the Recovery code leaves behind', () => {
  it('never reaches the API, storage, a log line or the screen after it is made', async () => {
    const logs = [
      jest.spyOn(console, 'log'),
      jest.spyOn(console, 'warn'),
      jest.spyOn(console, 'error'),
    ];
    const user = await renderRecoveryStep();
    const code = codeText();
    await emailTheCode(user);
    await user.press(savedBox());
    await user.press(continueButton());

    expect(api.post).not.toHaveBeenCalled();
    expect(JSON.stringify(secureStoreContents())).not.toContain(code);
    expect(await readFamilyKey(FAKE_UID)).toBeNull();
    expect(secureStoreContents()).toEqual({});
    for (const log of logs) expect(log).not.toHaveBeenCalled();
    expect(screen.queryByText(new RegExp(code))).not.toBeOnTheScreen();
  });
});
