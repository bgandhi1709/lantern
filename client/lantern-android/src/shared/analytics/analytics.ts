import {
  getAnalytics,
  logEvent,
  logScreenView,
  setAnalyticsCollectionEnabled,
} from '@react-native-firebase/analytics';
import { getPerformance } from '@react-native-firebase/perf';

import {
  REGISTRATION_STEPS,
  SCREEN_NAMES,
  TAP_NAMES,
  type RegistrationState,
  type RegistrationStep,
  type ScreenName,
  type TapName,
} from './events';

const analytics = getAnalytics();
const performance = getPerformance();

let collecting = false;

const isListed = <T extends string>(list: readonly T[], name: string): name is T =>
  list.includes(name as T);

// Collection is off from install; the app switches it on only for a Parent who agreed to the notice.
export function setCollectionEnabled(enabled: boolean) {
  collecting = enabled;
  performance.dataCollectionEnabled = enabled;
  return setAnalyticsCollectionEnabled(analytics, enabled);
}

export function trackScreenView(screen: ScreenName) {
  if (!collecting || !isListed(SCREEN_NAMES, screen)) return;
  void logScreenView(analytics, { screen_name: screen, screen_class: screen });
}

export function trackScreenTime(screen: ScreenName, seconds: number) {
  if (!collecting || !isListed(SCREEN_NAMES, screen)) return;
  void logEvent(analytics, 'screen_time', { screen_name: screen, seconds });
}

export function trackTap(name: TapName) {
  if (!collecting || !isListed(TAP_NAMES, name)) return;
  void logEvent(analytics, 'tap', { name });
}

export function trackRegistrationStep(step: RegistrationStep, state: RegistrationState) {
  if (!collecting || !isListed(REGISTRATION_STEPS, step)) return;
  void logEvent(analytics, 'registration_step', { step, state });
}
