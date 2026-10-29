import { trackRegistrationStep, trackTap } from '../shared/analytics';
import type { RegistrationStep, TapName } from '../shared/analytics';

// The Continue handler of a step: report the tap and the finished step, then move on.
export const finishStep = (step: RegistrationStep, tap: TapName, next: () => void) => () => {
  trackTap(tap);
  trackRegistrationStep(step, 'finished');
  next();
};
