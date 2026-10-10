import type { ReactNode } from 'react';

import { signOutOfLantern } from './googleSignIn';
import { SignedInView } from './SignedInView';
import type { Intro } from './useIntro';
import { useMeStatus } from './useMeStatus';

// Asks the API who the Parent is. Opening stays up until it answers, so Start never flashes.
export function SignedInRoute({ intro, home }: { intro: Intro; home: ReactNode }) {
  const { status, retry } = useMeStatus();

  return (
    <SignedInView
      status={status}
      intro={intro}
      home={home}
      onTryAgain={retry}
      onSignOut={signOutOfLantern}
    />
  );
}
