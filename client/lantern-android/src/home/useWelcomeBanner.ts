import { useCallback, useEffect, useState } from 'react';

import { readValue, writeValue } from '../shared/storage';

export function useWelcomeBanner() {
  const [dismissed, setDismissed] = useState<boolean | 'unknown'>('unknown');

  useEffect(() => {
    let cancelled = false;
    readValue('welcomeDismissed').then((value) => {
      if (!cancelled) setDismissed(value);
    });
    return () => {
      cancelled = true;
    };
  }, []);

  const dismiss = useCallback(() => {
    setDismissed(true);
    writeValue('welcomeDismissed', true).catch(() => undefined);
  }, []);

  return { visible: dismissed === false, dismiss };
}
