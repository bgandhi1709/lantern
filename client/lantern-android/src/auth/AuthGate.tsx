import type { ReactNode } from 'react';

import { FadeIn } from '../shared/ui/FadeIn';
import { useSessionState } from './AuthProvider';
import { OpeningScreen } from './OpeningScreen';
import { StartScreen } from './StartScreen';
import { useIntro } from './useIntro';

// The Opening screen stays until the saved sign-in is restored and the intro has played.
export function AuthGate({ signedIn }: { signedIn: ReactNode }) {
  const state = useSessionState();
  const intro = useIntro();

  if (state === 'restoring' || !intro.done) return <OpeningScreen intro={intro} />;
  return <FadeIn still={intro.still}>{state === 'signedIn' ? signedIn : <StartScreen />}</FadeIn>;
}
