import { useIsFocused } from 'expo-router';
import { useEffect, useRef } from 'react';

// A locked Home asks again when the Parent comes back from another screen, which may have opened the Family.
export function useRetryOnReturn(locked: boolean, retry: () => void) {
  const focused = useIsFocused();
  const wasAway = useRef(false);

  useEffect(() => {
    if (!focused) {
      wasAway.current = true;
      return;
    }
    if (wasAway.current && locked) retry();
    wasAway.current = false;
  }, [focused, locked, retry]);
}
