import type { ReactNode } from 'react';

import { SignedInView } from './SignedInView';
import type { Intro } from './useIntro';
import { useMeStatus } from './useMeStatus';

type Props = { intro: Intro; home: ReactNode; registration: ReactNode };

// Asks the API who the Parent is. Opening stays up until it answers, so Start never flashes.
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
