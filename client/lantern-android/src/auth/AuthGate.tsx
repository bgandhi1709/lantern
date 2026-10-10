import type { ReactNode } from 'react';

import { FadeIn } from '../shared/ui/FadeIn';
import { useSessionState } from './AuthProvider';
import { OpeningScreen } from './OpeningScreen';
import { SignedInRoute } from './SignedInRoute';
import { StartScreen } from './StartScreen';
import { useIntro } from './useIntro';

// The Opening screen stays until the saved sign-in is restored and the intro has played. A
// signed-in Parent then waits on the API's answer before Home, registration or Try again.
export function AuthGate({ home }: { home: ReactNode }) {
  const state = useSessionState();
  const intro = useIntro();

  if (state === 'restoring') return <OpeningScreen intro={intro} />;
  if (state === 'signedIn') return <SignedInRoute intro={intro} home={home} />;
  if (!intro.done) return <OpeningScreen intro={intro} />;
  return (
    <FadeIn still={intro.still}>
      <StartScreen />
    </FadeIn>
  );
}
