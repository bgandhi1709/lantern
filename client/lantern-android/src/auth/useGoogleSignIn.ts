import { useCallback, useState } from 'react';

import { SignInError, signInWithGoogle } from './googleSignIn';

export type SignInStatus = 'idle' | 'signingIn' | 'failed';

export function useGoogleSignIn() {
  const [status, setStatus] = useState<SignInStatus>('idle');

  const signIn = useCallback(async () => {
    setStatus('signingIn');
    try {
      await signInWithGoogle();
      setStatus('idle');
    } catch (error) {
      const cancelled = error instanceof SignInError && error.reason === 'cancelled';
      setStatus(cancelled ? 'idle' : 'failed');
    }
  }, []);

  return { status, signIn };
}
