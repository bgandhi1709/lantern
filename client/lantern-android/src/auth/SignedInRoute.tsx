import type { ReactNode } from 'react';

import { SignedInView } from './SignedInView';
import type { Intro } from './useIntro';
import { useMeStatus } from './useMeStatus';

// Asks the API who the Parent is. Opening stays up until it answers, so Start never flashes.
type Props = { intro: Intro; home: ReactNode; registration: ReactNode };

export function SignedInRoute({ intro, home, registration }: Props) {
  const { status, retry } = useMeStatus();

  return (
    <SignedInView
      status={status}
      intro={intro}
      home={home}
      registration={registration}
      onTryAgain={retry}
    />
  );
}
