import { useCallback, useEffect, useState } from 'react';

import { readValue, writeValue } from '../shared/storage';

// Hidden until storage has answered, so a Parent who dismissed it never sees it flash.
export function useWelcomeBanner() {
  const [dismissed, setDismissed] = useState<boolean | null>(null);

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
