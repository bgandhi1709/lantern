import { useEffect } from 'react';

import { trackRegistrationStep, trackTap, useTrackScreen } from '../shared/analytics';
import type { RegistrationStep, TapName } from '../shared/analytics';

// A registration step is also a screen. Returns the Continue handler that reports the tap and the
// finished step before moving on.
export function useRegistrationStep(step: RegistrationStep) {
  useTrackScreen(step);
  useEffect(() => {
    trackRegistrationStep(step, 'reached');
  }, [step]);

  return (tap: TapName, next: () => void) => () => {
    trackTap(tap);
    trackRegistrationStep(step, 'finished');
    next();
  };
}
