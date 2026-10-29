import { render } from '@testing-library/react-native';

import { logEvent, logScreenView } from '../../test/fakeFirebaseAnalytics';
import { setCollectionEnabled } from './analytics';
import { RouteTracker } from './RouteTracker';

let mockSegments: string[] = [];
jest.mock('expo-router', () => ({ useSegments: () => mockSegments }));

const viewed = () => logScreenView.mock.calls.map(([, params]) => params.screen_name);
const screenTimes = () =>
  logEvent.mock.calls.filter(([, name]) => name === 'screen_time').map(([, , params]) => params);

beforeEach(() => {
  jest.useFakeTimers({ now: 0 });
  mockSegments = [];
  setCollectionEnabled(true);
});

afterEach(() => jest.useRealTimers());

describe('the route in front', () => {
  it.each([
    [['settings'], 'appearance'],
    [['add-child'], 'add-child'],
    [['unlock'], 'unlock'],
    [['start-again'], 'start-again'],
    [['coming-next'], 'coming-next'],
    [['subject', '[subjectId]'], 'chapters'],
    [['chapter', '[chapterId]', 'history'], 'chapter-history'],
  ])('%j is counted as the %s screen', async (segments, screen) => {
    mockSegments = segments;

    await render(<RouteTracker />);

    expect(viewed()).toEqual([screen]);
  });

  it('is not counted for the front door, which Home and registration report themselves', async () => {
    await render(<RouteTracker />);

    expect(viewed()).toEqual([]);
  });

  it('is not counted for a route it does not know', async () => {
    mockSegments = ['somewhere-new'];

    await render(<RouteTracker />);

    expect(viewed()).toEqual([]);
  });

  it('sends the seconds on a screen when the Parent moves to the next one', async () => {
    mockSegments = ['settings'];
    const { rerender } = await render(<RouteTracker />);

    jest.advanceTimersByTime(6_000);
    mockSegments = ['unlock'];
    await rerender(<RouteTracker />);

    expect(screenTimes()).toEqual([{ screen_name: 'appearance', seconds: 6 }]);
    expect(viewed()).toEqual(['appearance', 'unlock']);
  });
});
