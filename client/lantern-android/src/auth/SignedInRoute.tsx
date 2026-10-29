import { useLayoutEffect } from 'react';
import type { ReactNode } from 'react';

import { setCollectionEnabled } from '../shared/analytics';
import { useRegistrationCheck } from './AuthProvider';
import { RegisteredProvider } from './RegisteredContext';
import { SignedInView } from './SignedInView';
import type { Intro } from './useIntro';
import { useMeStatus } from './useMeStatus';

type Props = { intro: Intro; home: ReactNode; registration: ReactNode };

// Asks the API who the Parent is. Opening stays up until it answers, so Start never flashes.
export function SignedInRoute({ intro, home, registration }: Props) {
  const { status, retry, markRegistered } = useMeStatus(useRegistrationCheck().epoch);

  // A registered Parent agreed to the notice when registering; a new one agrees on About you. A
  // layout effect, so it runs before Home's own effect reports that Home was viewed.
  useLayoutEffect(() => {
    if (status === 'registered' || status === 'notRegistered') {
      setCollectionEnabled(status === 'registered');
    }
  }, [status]);

  return (
    <RegisteredProvider value={markRegistered}>
      <SignedInView
        status={status}
        intro={intro}
        home={home}
        registration={registration}
        onTryAgain={retry}
      />
    </RegisteredProvider>
  );
}
