import { useIsFocused } from 'expo-router';
import { useEffect, useRef } from 'react';

import { readValue } from '../shared/storage';
import type { HomeChild } from './useHome';

export function useActiveChildOnReturn(
  activeId: string | null,
  children: HomeChild[],
  switchTo: (childId: string) => void,
) {
  const focused = useIsFocused();
  const wasAway = useRef(false);
  const latest = useRef({ activeId, children, switchTo });
  useEffect(() => {
    latest.current = { activeId, children, switchTo };
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
        const { activeId: current, children: known, switchTo: pick } = latest.current;
        if (storedId && storedId !== current && known.some((child) => child.id === storedId)) {
          pick(storedId);
        }
      })
      .catch(() => undefined);
  }, [focused]);
}
