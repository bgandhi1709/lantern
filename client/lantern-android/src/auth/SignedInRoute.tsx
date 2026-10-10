import type { ReactNode } from 'react';

import { useStrings } from '../shared/i18n';
import { FadeIn } from '../shared/ui/FadeIn';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { signOutOfLantern } from './googleSignIn';
import { OpeningScreen } from './OpeningScreen';
import type { Intro } from './useIntro';
import { type MeStatus, useMeStatus } from './useMeStatus';

// Asks the API who the Parent is. Opening stays up until it answers, so Start never flashes.
export function SignedInRoute({ intro, home }: { intro: Intro; home: ReactNode }) {
  const { status, retry } = useMeStatus();
  const strings = useStrings();

  const screens: Record<MeStatus, ReactNode> = {
    checking: <OpeningScreen intro={intro} />,
    waking: <OpeningScreen intro={intro} message={strings.opening.waking} />,
    registered: <FadeIn still={intro.still}>{home}</FadeIn>,
    notRegistered: (
      <OpeningScreen intro={intro} message={strings.opening.notRegistered}>
        <PrimaryButton label={strings.common.signOut} onPress={signOutOfLantern} />
      </OpeningScreen>
    ),
    unreachable: (
      <OpeningScreen intro={intro} message={strings.opening.unreachable}>
        <PrimaryButton label={strings.opening.tryAgain} onPress={retry} />
      </OpeningScreen>
    ),
  };

  if (!intro.done) return screens[status === 'waking' ? 'waking' : 'checking'];
  return screens[status];
}
