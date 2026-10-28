import { useIsFocused } from 'expo-router';
import { useEffect, useRef } from 'react';

import { readValue } from '../shared/storage';
import type { HomeChild } from './useHome';

export function useActiveChildOnReturn(
  activeId: string | null,
  children: HomeChild[],
  switchTo: (childId: string) => void,
  reload: () => void,
) {
  const focused = useIsFocused();
  const wasAway = useRef(false);
  const latest = useRef({ activeId, children, switchTo, reload });
  useEffect(() => {
    latest.current = { activeId, children, switchTo, reload };
  });

  useEffect(() => {
    if (!focused) {
      wasAway.current = true;
      return;
    }
    if (!wasAway.current) return;
    wasAway.current = false;
    readValue('activeChildId')
      .then((storedId) => {
        const {
          activeId: current,
          children: known,
          switchTo: pick,
          reload: again,
        } = latest.current;
        if (!storedId || storedId === current) return;
        if (known.some((child) => child.id === storedId)) pick(storedId);
        else again();
      })
      .catch(() => undefined);
  }, [focused]);
}
