import { useCallback, useEffect, useState } from 'react';

import { config } from './config';

export type ApiStatus = 'checking' | 'waking' | 'reachable' | 'unreachable';

// UAT scales to zero: the first call after idle takes about 25 s.
const TIMEOUT_MS = 45_000;
const WAKING_AFTER_MS = 3_000;

export function useApiReachable() {
  const [status, setStatus] = useState<ApiStatus>('checking');
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    let cancelled = false;
    const finish = (next: ApiStatus) => {
      if (!cancelled) setStatus(next);
    };
    const waking = setTimeout(() => finish('waking'), WAKING_AFTER_MS);
    const timeout = setTimeout(() => controller.abort(), TIMEOUT_MS);

    fetch(`${config.apiBaseUrl}/health/live`, { signal: controller.signal })
      .then((response) => finish(response.ok ? 'reachable' : 'unreachable'))
      .catch(() => finish('unreachable'))
      .finally(() => {
        clearTimeout(waking);
        clearTimeout(timeout);
      });

    return () => {
      cancelled = true;
      controller.abort();
      clearTimeout(waking);
      clearTimeout(timeout);
    };
  }, [attempt]);

  const retry = useCallback(() => {
    setStatus('checking');
    setAttempt((n) => n + 1);
  }, []);
  return { status, retry };
}
