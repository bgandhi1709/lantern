import '@testing-library/react-native/matchers';
import { act, render, screen } from '@testing-library/react-native';
import { AccessibilityInfo, Text } from 'react-native';

import { readValue, writeValue } from '../shared/storage';
import { AuthGate, AuthProvider } from '.';

const renderApp = () =>
  render(
    <AuthProvider>
      <AuthGate signedIn={<Text>Home</Text>} />
    </AuthProvider>,
  );

const startHeading = () =>
  screen.queryByRole('header', { name: 'Teach your child with confidence.' });
const wait = (ms: number) => act(async () => void jest.advanceTimersByTime(ms));

describe('the Opening screen', () => {
  beforeEach(() => {
    jest.useFakeTimers();
    jest.spyOn(AccessibilityInfo, 'isReduceMotionEnabled').mockResolvedValue(false);
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('shows the mark and says Lantern is opening while the intro plays', async () => {
    await renderApp();

    expect(
      screen.getByRole('image', { name: 'A mother teaching her child from an open book' }),
    ).toBeOnTheScreen();
    expect(screen.getByLabelText('Opening Lantern')).toBeOnTheScreen();
    expect(startHeading()).not.toBeOnTheScreen();
  });

  it('plays the full intro on a new install, then shows Start and remembers it was seen', async () => {
    await renderApp();

    await wait(1500);
    expect(startHeading()).not.toBeOnTheScreen();

    await wait(1500);
    expect(startHeading()).toBeOnTheScreen();
    expect(await readValue('introSeen')).toBe(true);
  });

  it('plays the short intro once the intro has been seen', async () => {
    await writeValue('introSeen', true);
    await renderApp();

    await wait(500);
    expect(startHeading()).not.toBeOnTheScreen();

    await wait(700);

    expect(startHeading()).toBeOnTheScreen();
  });

  it('skips the animation when the phone asks for less motion', async () => {
    jest.spyOn(AccessibilityInfo, 'isReduceMotionEnabled').mockResolvedValue(true);
    await renderApp();

    await wait(0);

    expect(startHeading()).toBeOnTheScreen();
  });
});
