import {
  logEvent,
  logScreenView,
  setAnalyticsCollectionEnabled,
} from '../../test/fakeFirebaseAnalytics';
import { performance } from '../../test/fakeFirebasePerf';
import {
  setCollectionEnabled,
  trackRegistrationStep,
  trackScreenTime,
  trackScreenView,
  tapped,
  trackTap,
} from './analytics';

const sendAll = () => {
  trackScreenView('home');
  trackScreenTime('home', 12);
  trackTap('open-settings');
  trackRegistrationStep('about-you', 'reached');
};

beforeEach(() => setCollectionEnabled(false));

describe('at every launch', () => {
  it('switches collection off, whatever the last launch left on in the SDK', () => {
    jest.isolateModules(() => {
      jest.requireActual('./analytics');
      const fake = jest.requireMock('@react-native-firebase/analytics');

      expect(fake.setAnalyticsCollectionEnabled).toHaveBeenCalledWith(expect.anything(), false);
    });
  });
});

describe('before the Parent agrees', () => {
  it('sends no event', () => {
    sendAll();

    expect(logEvent).not.toHaveBeenCalled();
    expect(logScreenView).not.toHaveBeenCalled();
  });

  it('keeps Analytics and Performance Monitoring collection off', () => {
    expect(setAnalyticsCollectionEnabled).toHaveBeenLastCalledWith(expect.anything(), false);
    expect(performance.dataCollectionEnabled).toBe(false);
  });
});

describe('after the Parent agrees', () => {
  beforeEach(() => setCollectionEnabled(true));

  it('switches collection on in Analytics and Performance Monitoring', () => {
    expect(setAnalyticsCollectionEnabled).toHaveBeenLastCalledWith(expect.anything(), true);
    expect(performance.dataCollectionEnabled).toBe(true);
  });

  it('sends a screen view, the time on it, a tap and a registration step with names only', () => {
    sendAll();

    expect(logScreenView).toHaveBeenCalledWith(expect.anything(), {
      screen_name: 'home',
      screen_class: 'home',
    });
    expect(logEvent).toHaveBeenCalledWith(expect.anything(), 'screen_time', {
      screen_name: 'home',
      seconds: 12,
    });
    expect(logEvent).toHaveBeenCalledWith(expect.anything(), 'tap', {
      name: 'open-settings',
    });
    expect(logEvent).toHaveBeenCalledWith(expect.anything(), 'registration_step', {
      step: 'about-you',
      state: 'reached',
    });
  });

  it('counts a tap and then does what the control does, with its arguments and result', () => {
    const open = jest.fn((id: string) => `opened ${id}`);

    const result = tapped('open-subject', open)('a-subject');

    expect(result).toBe('opened a-subject');
    expect(logEvent).toHaveBeenCalledWith(expect.anything(), 'tap', { name: 'open-subject' });
  });

  it('refuses a name that is not on the fixed lists', () => {
    // @ts-expect-error a screen name outside the list does not compile
    trackScreenView('ashas-home');
    // @ts-expect-error a tap name outside the list does not compile
    trackTap('asha.verma@gmail.com');
    // @ts-expect-error a step outside the list does not compile
    trackRegistrationStep('cbse', 'reached');

    expect(logEvent).not.toHaveBeenCalled();
    expect(logScreenView).not.toHaveBeenCalled();
  });

  it('stops sending when the Parent signs out', () => {
    setCollectionEnabled(false);
    sendAll();

    expect(logEvent).not.toHaveBeenCalled();
  });
});
