import { useEffect, useRef, useState } from 'react';

import { useMarkRegistered } from '../auth';
import { registerFamily } from './registerFamily';
import type { RegisterInput } from './registerFamily';
import { failureOf } from './registrationError';
import type { RegisterFailure } from './registrationError';

// One Finish at a time, and a result that arrives after the Parent left the step is dropped.
export function useRegisterFamily() {
  const markRegistered = useMarkRegistered();
  const [finishing, setFinishing] = useState(false);
  const [failure, setFailure] = useState<RegisterFailure | null>(null);
  const busy = useRef(false);
  const mounted = useRef(true);
  useEffect(
    () => () => {
      mounted.current = false;
    },
    [],
  );

  const finish = async (input: RegisterInput) => {
    if (busy.current) return;
    busy.current = true;
    setFinishing(true);
    setFailure(null);
    try {
      await registerFamily(input);
      if (!mounted.current) return;
      markRegistered();
    } catch (error) {
      if (!mounted.current) return;
      setFailure(failureOf(error));
      setFinishing(false);
    } finally {
      busy.current = false;
    }
  };

  return {
    finishing,
    failure,
    finish,
    isBusy: () => busy.current,
    clearFailure: () => setFailure(null),
  };
}
