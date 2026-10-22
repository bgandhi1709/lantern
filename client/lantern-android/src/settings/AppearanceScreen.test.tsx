import '@testing-library/react-native/matchers';
import { render, screen, userEvent } from '@testing-library/react-native';

import { ThemeProvider } from '../shared/theme';
import { clearAll, keepFamilyKey, readValue, writeValue } from '../shared/storage';
import { FAKE_UID, fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { AppearanceScreen } from './AppearanceScreen';

const onBack = jest.fn();
const onUnlock = jest.fn();

async function openAppearance() {
  const user = userEvent.setup();
  await render(
    <ThemeProvider>
      <AppearanceScreen onBack={onBack} onUnlock={onUnlock} />
    </ThemeProvider>,
  );
  await screen.findByRole('header', { name: 'Appearance' });
  return user;
}

const choice = (name: string) => screen.getByRole('radio', { name });

beforeEach(async () => {
  await clearAll();
  onBack.mockReset();
  onUnlock.mockReset();
  fakeRestoredSession();
});

describe('Appearance', () => {
  it('shows the three choices with the current look picked', async () => {
    await openAppearance();

    expect(screen.getByLabelText('Theme')).toBeOnTheScreen();
    expect(screen.getByLabelText('Text size')).toBeOnTheScreen();
    expect(screen.getByLabelText('Colour')).toBeOnTheScreen();
    expect(choice('Light')).toBeChecked();
    expect(screen.queryByLabelText('Match phone')).not.toBeOnTheScreen();
    expect(choice('Normal text size')).toBeChecked();
    expect(choice('Teal. Default. Calm.')).toBeChecked();
    expect(choice('Dark')).not.toBeChecked();
  });

  it('opens with what the Parent picked last time', async () => {
    await writeValue('theme', 'dark');
    await writeValue('textSize', 'largest');
    await writeValue('colourScheme', 'golden');

    await openAppearance();

    expect(choice('Dark')).toBeChecked();
    expect(choice('Largest text size')).toBeChecked();
    expect(choice('Golden. Warm lamplight.')).toBeChecked();
  });

  it('picks a theme and keeps it on the phone', async () => {
    const user = await openAppearance();

    await user.press(choice('Dark'));

    expect(choice('Dark')).toBeChecked();
    expect(choice('Light')).not.toBeChecked();
    expect(await readValue('theme')).toBe('dark');
  });

  it('picks a text size and keeps it on the phone', async () => {
    const user = await openAppearance();

    await user.press(choice('Large text size'));

    expect(choice('Large text size')).toBeChecked();
    expect(await readValue('textSize')).toBe('large');
  });

  it('picks a colour and keeps it on the phone', async () => {
    const user = await openAppearance();

    await user.press(choice('Blue. Classic.'));

    expect(choice('Blue. Classic.')).toBeChecked();
    expect(choice('Teal. Default. Calm.')).not.toBeChecked();
    expect(await readValue('colourScheme')).toBe('blue');
  });

  it('previews a Chapter card and says the choices stay on this phone', async () => {
    await openAppearance();

    expect(screen.getByText('Preview')).toBeOnTheScreen();
    expect(screen.getByText('Transparent, translucent and opaque')).toBeOnTheScreen();
    expect(
      screen.getByText('Saved on this phone only. Signing out keeps these.'),
    ).toBeOnTheScreen();
  });

  it('goes back to Home', async () => {
    const user = await openAppearance();

    await user.press(screen.getByRole('button', { name: 'Back' }));

    expect(onBack).toHaveBeenCalledTimes(1);
  });

  it('offers Unlock your Family while this phone has no Family key, and opens it', async () => {
    const user = await openAppearance();

    await user.press(await screen.findByRole('button', { name: 'Unlock your Family' }));

    expect(onUnlock).toHaveBeenCalledTimes(1);
  });

  it('offers nothing to unlock once this phone holds the Family key', async () => {
    await keepFamilyKey(FAKE_UID, 'key');

    await openAppearance();

    expect(screen.queryByRole('button', { name: 'Unlock your Family' })).not.toBeOnTheScreen();
  });

  it('offers nothing to unlock for an account whose Family key is another account’s', async () => {
    await keepFamilyKey('someone-else', 'key');

    await openAppearance();

    expect(await screen.findByRole('button', { name: 'Unlock your Family' })).toBeOnTheScreen();
  });
});
