import { useEffect, useRef } from 'react';

export function useIsMounted() {
  const mounted = useRef(true);
  useEffect(
    () => () => {
      mounted.current = false;
    },
    [],
  );
  return mounted;
}
