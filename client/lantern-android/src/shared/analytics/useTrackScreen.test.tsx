import { renderHook } from '@testing-library/react-native';
import { AppState, type AppStateStatus } from 'react-native';

import { logEvent, logScreenView } from '../../test/fakeFirebaseAnalytics';
import { setCollectionEnabled } from './analytics';
import { useTrackScreen } from './useTrackScreen';

let mockFocused = true;
jest.mock('expo-router', () => ({ useIsFocused: () => mockFocused }));

let appStateListener: (state: AppStateStatus) => void = () => {};
const removeListener = jest.fn();

const screenTimes = () =>
  logEvent.mock.calls.filter(([, name]) => name === 'screen_time').map(([, , params]) => params);

beforeEach(() => {
  jest.useFakeTimers({ now: 0 });
  mockFocused = true;
  setCollectionEnabled(true);
  jest.spyOn(AppState, 'addEventListener').mockImplementation((_type, listener) => {
    appStateListener = listener;
    return { remove: removeListener };
  });
});

afterEach(() => jest.useRealTimers());

describe('a Parent on a screen', () => {
  it('is counted as viewing it when it opens', async () => {
    await renderHook(() => useTrackScreen('about-you'));

    expect(logScreenView).toHaveBeenCalledWith(expect.anything(), {
      screen_name: 'about-you',
      screen_class: 'about-you',
    });
  });

  it('has the seconds they spent there sent when they leave', async () => {
    const { unmount } = await renderHook(() => useTrackScreen('about-you'));

    jest.advanceTimersByTime(12_400);
    await unmount();

    expect(screenTimes()).toEqual([{ screen_name: 'about-you', seconds: 12 }]);
  });

  it('does not count the time the app spent in the background', async () => {
    const { unmount } = await renderHook(() => useTrackScreen('home'));

    jest.advanceTimersByTime(5_000);
    appStateListener('background');
    jest.advanceTimersByTime(600_000);
    appStateListener('active');
    jest.advanceTimersByTime(3_000);
    await unmount();

    expect(screenTimes()).toEqual([
      { screen_name: 'home', seconds: 5 },
      { screen_name: 'home', seconds: 3 },
    ]);
  });

  it('is counted once when the phone goes inactive and then background', async () => {
    const { unmount } = await renderHook(() => useTrackScreen('home'));

    jest.advanceTimersByTime(4_000);
    appStateListener('inactive');
    appStateListener('background');
    await unmount();

    expect(screenTimes()).toEqual([{ screen_name: 'home', seconds: 4 }]);
  });

  it('stops listening to the app state when they leave', async () => {
    const { unmount } = await renderHook(() => useTrackScreen('home'));

    await unmount();

    expect(removeListener).toHaveBeenCalled();
  });
});

describe('a screen another screen has opened over', () => {
  it('stops counting while covered and counts a new view when it is back in front', async () => {
    const { rerender } = await renderHook(() => useTrackScreen('home'));

    jest.advanceTimersByTime(7_000);
    mockFocused = false;
    await rerender({});
    jest.advanceTimersByTime(60_000);
    mockFocused = true;
    await rerender({});

    expect(screenTimes()).toEqual([{ screen_name: 'home', seconds: 7 }]);
    expect(logScreenView).toHaveBeenCalledTimes(2);
  });
});
