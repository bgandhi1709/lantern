import '@testing-library/react-native/matchers';
import { render, screen, userEvent } from '@testing-library/react-native';

import { ThemeProvider } from '../shared/theme';
import { clearAll, readValue, writeValue } from '../shared/storage';
import { AppearanceScreen } from './AppearanceScreen';

const onBack = jest.fn();

async function openAppearance() {
  const user = userEvent.setup();
  await render(
    <ThemeProvider>
      <AppearanceScreen onBack={onBack} />
    </ThemeProvider>,
  );
  await screen.findByRole('header', { name: 'Appearance' });
  return user;
}

const choice = (name: string) => screen.getByRole('radio', { name });

beforeEach(async () => {
  await clearAll();
  onBack.mockReset();
});

describe('Appearance', () => {
  it('shows the three choices with the current look picked', async () => {
    await openAppearance();

    expect(screen.getByLabelText('Theme')).toBeOnTheScreen();
    expect(screen.getByLabelText('Text size')).toBeOnTheScreen();
    expect(screen.getByLabelText('Colour')).toBeOnTheScreen();
    expect(choice('Match phone')).toBeChecked();
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
    expect(choice('Match phone')).not.toBeChecked();
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
      screen.getByText('Saved on this phone only. Signing out resets these.'),
    ).toBeOnTheScreen();
  });

  it('goes back to Home', async () => {
    const user = await openAppearance();

    await user.press(screen.getByRole('button', { name: 'Back' }));

    expect(onBack).toHaveBeenCalledTimes(1);
  });
});
