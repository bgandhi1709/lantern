import type { ReactNode } from 'react';

import { useStrings } from '../shared/i18n';
import { FadeIn } from '../shared/ui/FadeIn';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { OpeningScreen } from './OpeningScreen';
import type { Intro } from './useIntro';
import type { MeStatus } from './useMeStatus';

type Props = {
  status: MeStatus;
  intro: Intro;
  home: ReactNode;
  onTryAgain: () => void;
  onSignOut: () => void;
};

// What the signed-in Parent sees for each answer from the API. Opening stays up until the intro has played.
export function SignedInView({ status, intro, home, onTryAgain, onSignOut }: Props) {
  const strings = useStrings();

  const screens: Record<MeStatus, ReactNode> = {
    checking: <OpeningScreen intro={intro} />,
    waking: <OpeningScreen intro={intro} message={strings.opening.waking} />,
    registered: <FadeIn still={intro.still}>{home}</FadeIn>,
    notRegistered: (
      <OpeningScreen intro={intro} message={strings.opening.notRegistered}>
        <PrimaryButton label={strings.common.signOut} onPress={onSignOut} />
      </OpeningScreen>
    ),
    unreachable: (
      <OpeningScreen intro={intro} message={strings.opening.unreachable}>
        <PrimaryButton label={strings.opening.tryAgain} onPress={onTryAgain} />
      </OpeningScreen>
    ),
  };

  if (!intro.done) return screens[status === 'waking' ? 'waking' : 'checking'];
  return screens[status];
}
