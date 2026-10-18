import { act, render, screen } from '@testing-library/react-native';
import * as ReactNative from 'react-native';
import { Text } from 'react-native';

import { fakeRestoredSession } from '../../test/fakeFirebaseAuth';
import { endSession } from '../session';
import { clearAll, readValue, writeValue } from '../storage';
import { ThemeProvider } from './ThemeProvider';
import { useAppearance } from './useAppearance';

function Probe() {
  const { appearance, dark, setColourScheme, setTextSize, setTheme } = useAppearance();
  return (
    <>
      <Text>{`${appearance.theme}/${appearance.textSize}/${appearance.colourScheme}`}</Text>
      <Text>{dark ? 'dark' : 'light'}</Text>
      <Text onPress={() => setColourScheme('golden')}>pick golden</Text>
      <Text onPress={() => setTextSize('largest')}>pick largest</Text>
      <Text onPress={() => setTheme('dark')}>pick dark</Text>
    </>
  );
}

const openProbe = () =>
  render(
    <ThemeProvider>
      <Probe />
    </ThemeProvider>,
  );

beforeEach(async () => {
  await clearAll();
  jest.spyOn(ReactNative, 'useColorScheme').mockReturnValue('light');
});

describe('the look of the app', () => {
  it('starts as Match phone, the normal text size and Teal', async () => {
    await openProbe();

    expect(await screen.findByText('system/default/teal')).toBeOnTheScreen();
  });

  it('opens with what the Parent chose last time', async () => {
    await writeValue('colourScheme', 'blue');
    await writeValue('theme', 'dark');
    await writeValue('textSize', 'large');

    await openProbe();

    expect(await screen.findByText('dark/large/blue')).toBeOnTheScreen();
  });

  it('saves each choice on the phone', async () => {
    await openProbe();

    await act(async () => screen.getByText('pick golden').props.onPress());
    await act(async () => screen.getByText('pick largest').props.onPress());
    await act(async () => screen.getByText('pick dark').props.onPress());

    expect(screen.getByText('dark/largest/golden')).toBeOnTheScreen();
    expect(await readValue('colourScheme')).toBe('golden');
    expect(await readValue('textSize')).toBe('largest');
    expect(await readValue('theme')).toBe('dark');
  });

  it('follows the phone while the theme is Match phone', async () => {
    jest.spyOn(ReactNative, 'useColorScheme').mockReturnValue('dark');

    await openProbe();

    expect(await screen.findByText('dark')).toBeOnTheScreen();
  });

  it('keeps the look when the Parent signs out', async () => {
    fakeRestoredSession();
    await writeValue('colourScheme', 'blue');
    await openProbe();
    await screen.findByText('system/default/blue');

    await act(async () => endSession());

    expect(screen.getByText('system/default/blue')).toBeOnTheScreen();
    expect(await readValue('colourScheme')).toBe('blue');
  });
});
