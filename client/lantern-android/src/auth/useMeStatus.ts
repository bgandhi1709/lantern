import { useCallback, useEffect, useState } from 'react';
import { z } from 'zod';

import { ApiError, api } from '../shared/api';

export type MeStatus = 'checking' | 'waking' | 'registered' | 'notRegistered' | 'unreachable';

// UAT scales to zero: after this long the screen says so instead of looking stuck.
const WAKING_AFTER_MS = 3_000;

export function useMeStatus() {
  const [status, setStatus] = useState<MeStatus>('checking');
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let cancelled = false;
    const finish = (next: MeStatus) => {
      if (!cancelled) setStatus(next);
    };
    const waking = setTimeout(() => finish('waking'), WAKING_AFTER_MS);

    api
      .get('/v1/me', z.unknown())
      .then(() => finish('registered'))
      .catch((error) =>
        finish(
          error instanceof ApiError && error.code === 'not-registered'
            ? 'notRegistered'
            : 'unreachable',
        ),
      )
      .finally(() => clearTimeout(waking));

    return () => {
      cancelled = true;
      clearTimeout(waking);
    };
  }, [attempt]);

  const retry = useCallback(() => {
    setStatus('checking');
    setAttempt((n) => n + 1);
  }, []);

  return { status, retry };
}
