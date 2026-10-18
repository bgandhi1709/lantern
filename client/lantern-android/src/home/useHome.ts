import { useEffect, useState } from 'react';

import { unlockText } from '../shared/crypto';
import { readValue } from '../shared/storage';
import { fetchChildren, fetchSubjects } from './homeApi';
import type { Subject } from './homeApi';

export type HomeChild = { id: string; name: string; classLevel: number };

export type SubjectsState = { status: 'loading' } | { status: 'ready'; items: Subject[] };

export function useHome() {
  const [activeChild, setActiveChild] = useState<HomeChild | null>(null);
  const [subjects, setSubjects] = useState<SubjectsState>({ status: 'loading' });

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      const familyKey = await readValue('familyKey');
      const [first] = await fetchChildren();
      if (!familyKey || !first) return;
      const child = {
        id: first.childId,
        name: unlockText(familyKey, first.name),
        classLevel: first.classLevel,
      };
      const items = await fetchSubjects(child.id);
      if (cancelled) return;
      setActiveChild(child);
      setSubjects({ status: 'ready', items });
    };
    load();
    return () => {
      cancelled = true;
    };
  }, []);

  return { activeChild, subjects };
}
