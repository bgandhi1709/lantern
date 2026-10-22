import { useRef, useState } from 'react';

import { startAgain } from './startAgain';
import { useIsMounted } from './useIsMounted';

export function useStartAgain(onDeleted: () => void) {
  const [deleting, setDeleting] = useState(false);
  const [failed, setFailed] = useState(false);
  const busy = useRef(false);
  const mounted = useIsMounted();

  const remove = async () => {
    if (busy.current) return;
    busy.current = true;
    setDeleting(true);
    setFailed(false);
    try {
      await startAgain();
      onDeleted();
    } catch {
      if (!mounted.current) return;
      setFailed(true);
      setDeleting(false);
    } finally {
      busy.current = false;
    }
  };

  return { deleting, failed, busy, remove };
}
