import '@testing-library/react-native/matchers';
import { render, screen, userEvent } from '@testing-library/react-native';

import { ThemeProvider } from '../shared/theme';
import { clearAll, keepFamilyKey, readValue, writeValue } from '../shared/storage';
import { FAKE_UID, fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { SettingsScreen } from './SettingsScreen';

const onBack = jest.fn();
const onUnlock = jest.fn();
const onStartAgain = jest.fn();
const onDeleteChild = jest.fn();
const onDeleteFamily = jest.fn();

async function openSettings() {
  const user = userEvent.setup();
  await render(
    <ThemeProvider>
      <SettingsScreen
        onBack={onBack}
        onUnlock={onUnlock}
        onStartAgain={onStartAgain}
        onDeleteChild={onDeleteChild}
        onDeleteFamily={onDeleteFamily}
      />
    </ThemeProvider>,
  );
  await screen.findByRole('header', { name: 'Settings' });
  return user;
}

const choice = (name: string) => screen.getByRole('radio', { name });
const row = (name: string, expanded = false) => screen.getByRole('button', { name, expanded });
const search = () => screen.getByLabelText('Search settings');

beforeEach(async () => {
  await clearAll();
  [onBack, onUnlock, onStartAgain, onDeleteChild, onDeleteFamily].forEach((mock) =>
    mock.mockReset(),
  );
  fakeRestoredSession();
});

describe('Settings outline', () => {
  it('opens with both groups showing and every setting collapsed with its current value', async () => {
    await openSettings();

    expect(row('App settings', true)).toBeOnTheScreen();
    expect(row('Theme, Light')).toBeOnTheScreen();
    expect(row('Text size, Normal text size')).toBeOnTheScreen();
    expect(row('Colour, Teal')).toBeOnTheScreen();
    expect(row('Preview, A lesson step')).toBeOnTheScreen();
    expect(screen.queryByRole('radio')).not.toBeOnTheScreen();
    expect(screen.queryByText('Transparent, translucent and opaque')).not.toBeOnTheScreen();
  });

  it('shows what the Parent picked last time as the current value', async () => {
    await writeValue('theme', 'dark');
    await writeValue('textSize', 'largest');
    await writeValue('colourScheme', 'golden');

    await openSettings();

    expect(row('Theme, Dark')).toBeOnTheScreen();
    expect(row('Text size, Largest text size')).toBeOnTheScreen();
    expect(row('Colour, Golden')).toBeOnTheScreen();
  });

  it('opens a setting and closes it again', async () => {
    const user = await openSettings();

    await user.press(row('Theme, Light'));

    expect(row('Theme, Light', true)).toBeOnTheScreen();
    expect(choice('Light')).toBeChecked();
    expect(choice('Dark')).not.toBeChecked();

    await user.press(row('Theme, Light', true));

    expect(screen.queryByRole('radio')).not.toBeOnTheScreen();
  });

  it('collapses a group and opens it again', async () => {
    const user = await openSettings();

    await user.press(row('App settings', true));

    expect(row('App settings')).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Theme, Light' })).not.toBeOnTheScreen();

    await user.press(row('App settings'));

    expect(row('Theme, Light')).toBeOnTheScreen();
  });

  it('picks a theme and keeps it on the phone', async () => {
    const user = await openSettings();
    await user.press(row('Theme, Light'));

    await user.press(choice('Dark'));

    expect(choice('Dark')).toBeChecked();
    expect(row('Theme, Dark', true)).toBeOnTheScreen();
    expect(await readValue('theme')).toBe('dark');
  });

  it('picks a text size and keeps it on the phone', async () => {
    const user = await openSettings();
    await user.press(row('Text size, Normal text size'));

    await user.press(choice('Large text size'));

    expect(choice('Large text size')).toBeChecked();
    expect(await readValue('textSize')).toBe('large');
  });

  it('picks a colour and keeps it on the phone', async () => {
    const user = await openSettings();
    await user.press(row('Colour, Teal'));

    await user.press(choice('Blue. Classic.'));

    expect(choice('Blue. Classic.')).toBeChecked();
    expect(choice('Teal. Default. Calm.')).not.toBeChecked();
    expect(await readValue('colourScheme')).toBe('blue');
  });

  it('previews a Chapter card and says the choices stay on this phone', async () => {
    const user = await openSettings();

    await user.press(row('Preview, A lesson step'));

    expect(screen.getByText('Transparent, translucent and opaque')).toBeOnTheScreen();
    expect(
      screen.getByText('Saved on this phone only. Signing out keeps these.'),
    ).toBeOnTheScreen();
  });

  it('goes back to Home', async () => {
    const user = await openSettings();

    await user.press(screen.getByRole('button', { name: 'Back' }));

    expect(onBack).toHaveBeenCalledTimes(1);
  });
});

describe('Settings search', () => {
  it('shows only the settings that match and opens them', async () => {
    const user = await openSettings();

    await user.type(search(), 'dark');

    expect(row('Theme, Light', true)).toBeOnTheScreen();
    expect(choice('Dark')).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: /^Text size/ })).not.toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Family management' })).not.toBeOnTheScreen();
  });

  it('matches every word typed, in any order, and ignores capitals', async () => {
    const user = await openSettings();

    await user.type(search(), 'SIZE text');

    expect(row('Text size, Normal text size', true)).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: /^Theme/ })).not.toBeOnTheScreen();
  });

  it('says so when nothing matches, and Clear search brings everything back', async () => {
    const user = await openSettings();

    await user.type(search(), 'zzz');

    expect(screen.getByText('No settings match "zzz"')).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'App settings' })).not.toBeOnTheScreen();

    await user.press(screen.getByRole('button', { name: 'Clear search' }));

    expect(search()).toHaveDisplayValue('');
    expect(row('App settings', true)).toBeOnTheScreen();
    expect(row('Theme, Light')).toBeOnTheScreen();
  });

  it('clears the search with the cross in the field', async () => {
    const user = await openSettings();
    await user.type(search(), 'dark');

    await user.press(screen.getByRole('button', { name: 'Clear the search box' }));

    expect(search()).toHaveDisplayValue('');
    expect(row('Text size, Normal text size')).toBeOnTheScreen();
    expect(screen.queryByRole('radio')).not.toBeOnTheScreen();
  });

  it('lets the Parent close a setting the search opened', async () => {
    const user = await openSettings();
    await user.type(search(), 'colour');

    await user.press(row('Colour, Teal', true));

    expect(row('Colour, Teal')).toBeOnTheScreen();
    expect(screen.queryByRole('radio')).not.toBeOnTheScreen();
  });
});

describe('Family management', () => {
  it('offers Unlock your Family while this phone has no Family key, and opens it', async () => {
    const user = await openSettings();

    await user.press(await screen.findByRole('button', { name: 'Unlock your Family' }));

    expect(onUnlock).toHaveBeenCalledTimes(1);
  });

  it('offers nothing to unlock once this phone holds the Family key', async () => {
    await keepFamilyKey(FAKE_UID, 'key');

    await openSettings();

    expect(screen.queryByRole('button', { name: 'Unlock your Family' })).not.toBeOnTheScreen();
  });

  it('offers nothing to unlock for an account whose Family key is another account’s', async () => {
    await keepFamilyKey('someone-else', 'key');

    await openSettings();

    expect(await screen.findByRole('button', { name: 'Unlock your Family' })).toBeOnTheScreen();
  });

  it('offers Start again in Settings while this phone has no Family key, and opens it', async () => {
    const user = await openSettings();

    await user.press(await screen.findByRole('button', { name: 'Start again' }));

    expect(onStartAgain).toHaveBeenCalledTimes(1);
  });

  it('offers no Start again once this phone holds the Family key', async () => {
    await keepFamilyKey(FAKE_UID, 'key');

    await openSettings();

    expect(screen.queryByRole('button', { name: 'Start again' })).not.toBeOnTheScreen();
  });

  it('offers the two deletes once this phone holds the Family key, and opens each', async () => {
    await keepFamilyKey(FAKE_UID, 'key');
    const user = await openSettings();

    await user.press(await screen.findByRole('button', { name: 'Delete a child' }));
    await user.press(screen.getByRole('button', { name: 'Delete Family' }));

    expect(onDeleteChild).toHaveBeenCalledTimes(1);
    expect(onDeleteFamily).toHaveBeenCalledTimes(1);
  });

  it('offers no delete while this phone has no Family key', async () => {
    await openSettings();
    await screen.findByRole('button', { name: 'Unlock your Family' });

    expect(screen.queryByRole('button', { name: 'Delete a child' })).not.toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: 'Delete Family' })).not.toBeOnTheScreen();
  });

  it('finds the deletes by searching for delete', async () => {
    await keepFamilyKey(FAKE_UID, 'key');
    const user = await openSettings();
    await screen.findByRole('button', { name: 'Delete a child' });

    await user.type(search(), 'erase');

    expect(screen.getByRole('button', { name: 'Delete a child' })).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Delete Family' })).toBeOnTheScreen();
    expect(screen.queryByRole('button', { name: /^Theme/ })).not.toBeOnTheScreen();
  });
});
