import '@testing-library/react-native/matchers';
import { act, render, screen, userEvent } from '@testing-library/react-native';
import { AccessibilityInfo, Text } from 'react-native';

import { ApiError, api } from '../shared/api';
import { fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { AuthGate, AuthProvider } from '.';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const get = api.get as jest.Mock;

const renderApp = () =>
  render(
    <AuthProvider>
      <AuthGate home={<Text>Home</Text>} />
    </AuthProvider>,
  );

const startButton = () => screen.queryByRole('button', { name: 'Continue with Google' });
const wait = (ms: number) => act(async () => void jest.advanceTimersByTime(ms));

describe('where the Opening screen sends the Parent', () => {
  beforeEach(() => {
    jest.useFakeTimers();
    get.mockReset();
    jest.spyOn(AccessibilityInfo, 'isReduceMotionEnabled').mockResolvedValue(true);
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('sends a registered Parent to Home without showing Start', async () => {
    fakeRestoredSession();
    get.mockResolvedValue({});

    await renderApp();
    await wait(0);

    expect(screen.getByText('Home')).toBeOnTheScreen();
    expect(startButton()).not.toBeOnTheScreen();
    expect(get.mock.calls[0][0]).toBe('/v1/me');
  });

  it('sends a signed-out Parent to Start without asking the API', async () => {
    await renderApp();
    await wait(0);

    expect(startButton()).toBeOnTheScreen();
    expect(get).not.toHaveBeenCalled();
  });

  it('keeps Opening up while the API answers, even after the intro has played', async () => {
    fakeRestoredSession();
    get.mockReturnValue(new Promise(() => undefined));

    await renderApp();
    await wait(2000);

    expect(screen.getByLabelText('Opening Lantern')).toBeOnTheScreen();
    expect(startButton()).not.toBeOnTheScreen();
    expect(screen.queryByText('Home')).not.toBeOnTheScreen();
  });

  it('says Lantern is waking up, not an error, when the API is slow to answer', async () => {
    fakeRestoredSession();
    let answer: (value: unknown) => void = () => undefined;
    get.mockReturnValue(new Promise((resolve) => (answer = resolve)));
    await renderApp();
    await wait(1000);
    expect(screen.queryByText(/Waking Lantern up/)).not.toBeOnTheScreen();

    await wait(3000);

    expect(screen.getByText(/Waking Lantern up/)).toBeOnTheScreen();
    expect(screen.queryByText("Can't reach Lantern")).not.toBeOnTheScreen();

    await act(async () => answer({}));

    expect(screen.getByText('Home')).toBeOnTheScreen();
  });

  it('tells a signed-in Parent who has not registered that registration comes next', async () => {
    fakeRestoredSession();
    get.mockRejectedValue(new ApiError('not-registered', 404));

    await renderApp();
    await wait(0);

    expect(screen.getByText('Registration comes next')).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeOnTheScreen();
    expect(screen.getByRole('image', { name: /mother teaching/ })).toBeOnTheScreen();
  });

  it('lets that Parent sign out to Start', async () => {
    fakeRestoredSession();
    get.mockRejectedValue(new ApiError('not-registered', 404));
    const user = userEvent.setup({ advanceTimers: jest.advanceTimersByTime });
    await renderApp();
    await wait(0);

    await user.press(screen.getByRole('button', { name: 'Sign out' }));
    await wait(0);

    expect(startButton()).toBeOnTheScreen();
  });

  it.each([
    ['no signal', new ApiError('network', null)],
    ['a timeout', new ApiError('timeout', null)],
    ['a server error', new ApiError('http-503', 503)],
  ])('stays signed in and offers Try again after %s', async (_name, failure) => {
    fakeRestoredSession();
    get.mockRejectedValueOnce(failure).mockResolvedValue({});
    const user = userEvent.setup({ advanceTimers: jest.advanceTimersByTime });
    await renderApp();
    await wait(0);

    expect(screen.getByText("Can't reach Lantern")).toBeOnTheScreen();
    expect(startButton()).not.toBeOnTheScreen();

    await user.press(screen.getByRole('button', { name: 'Try again' }));
    await wait(0);

    expect(screen.getByText('Home')).toBeOnTheScreen();
  });

  it('goes to Opening while it tries again, not back to an error', async () => {
    fakeRestoredSession();
    get.mockRejectedValueOnce(new ApiError('network', null));
    const user = userEvent.setup({ advanceTimers: jest.advanceTimersByTime });
    await renderApp();
    await wait(0);
    get.mockReturnValue(new Promise(() => undefined));

    await user.press(screen.getByRole('button', { name: 'Try again' }));

    expect(screen.queryByText("Can't reach Lantern")).not.toBeOnTheScreen();
    expect(screen.getByLabelText('Opening Lantern')).toBeOnTheScreen();
  });
});

describe('when the API answers before the intro has finished', () => {
  beforeEach(() => {
    jest.useFakeTimers();
    get.mockReset();
    jest.spyOn(AccessibilityInfo, 'isReduceMotionEnabled').mockResolvedValue(false);
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('holds Opening until the intro is done, then shows Home', async () => {
    fakeRestoredSession();
    get.mockResolvedValue({});

    await renderApp();
    await wait(1500);
    expect(screen.queryByText('Home')).not.toBeOnTheScreen();
    expect(screen.getByRole('image', { name: /mother teaching/ })).toBeOnTheScreen();

    await wait(1600);

    expect(screen.getByText('Home')).toBeOnTheScreen();
    expect(startButton()).not.toBeOnTheScreen();
  });

  it('holds Opening until the intro is done, then says registration comes next', async () => {
    fakeRestoredSession();
    get.mockRejectedValue(new ApiError('not-registered', 404));

    await renderApp();
    await wait(1500);
    expect(screen.queryByText('Registration comes next')).not.toBeOnTheScreen();

    await wait(1600);

    expect(screen.getByText('Registration comes next')).toBeOnTheScreen();
  });

  it('holds Opening until the intro is done, then offers Try again', async () => {
    fakeRestoredSession();
    get.mockRejectedValue(new ApiError('network', null));

    await renderApp();
    await wait(1500);
    expect(screen.queryByText("Can't reach Lantern")).not.toBeOnTheScreen();

    await wait(1600);

    expect(screen.getByText("Can't reach Lantern")).toBeOnTheScreen();
  });
});
