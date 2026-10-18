import { useEffect, useState } from 'react';

import { ApiError } from '../shared/api';
import { unlockText } from '../shared/crypto';
import { readValue } from '../shared/storage';
import { fetchChildren, fetchSubjects } from './homeApi';
import type { Subject } from './homeApi';

const CONTENT_POLL_MS = 3_000;

const isNotReady = (error: unknown) =>
  error instanceof ApiError && error.code === 'content-not-ready';

const wait = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));

async function subjectsOnceReady(childId: string, isCancelled: () => boolean) {
  for (;;) {
    try {
      return await fetchSubjects(childId);
    } catch (error) {
      if (!isNotReady(error) || isCancelled()) throw error;
      await wait(CONTENT_POLL_MS);
      if (isCancelled()) return [];
    }
  }
}

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
      const items = await subjectsOnceReady(child.id, () => cancelled);
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
