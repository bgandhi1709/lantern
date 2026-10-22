import { useCallback, useEffect, useState } from 'react';

import { getParent } from '../shared/session';
import { readFamilyKey, writeValue } from '../shared/storage';
import { fetchChapters } from './chaptersApi';
import type { Chapter } from './chaptersApi';
import { fetchSubjects } from './homeApi';
import type { Subject } from './homeApi';
import { fetchUnlockedChildren } from './unlockedChildren';
import type { HomeChild } from './useHome';

export type ChaptersState =
  { status: 'loading' } | { status: 'failed' } | { status: 'ready'; chapters: Chapter[] };

const LOADING: ChaptersState = { status: 'loading' };
const FAILED: ChaptersState = { status: 'failed' };

export function useChapters(initialChildId: string, initialSubjectId: string) {
  const [childId, setChildId] = useState(initialChildId);
  const [subjectId, setSubjectId] = useState<string | null>(initialSubjectId);
  const [children, setChildren] = useState<HomeChild[]>([]);
  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [chapters, setChapters] = useState<Chapter[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      const familyKey = await readFamilyKey(getParent().uid);
      if (!familyKey) throw new Error('This phone does not hold the Family key.');
      const list = await fetchUnlockedChildren(familyKey);
      if (!cancelled) setChildren(list);
    };
    load().catch(() => {
      if (!cancelled) setFailed(true);
    });
    return () => {
      cancelled = true;
    };
  }, [attempt]);

  useEffect(() => {
    let cancelled = false;
    fetchSubjects(childId)
      .then((list) => {
        if (cancelled) return;
        setSubjects(list);
        setSubjectId((current) =>
          list.some((subject) => subject.subjectId === current)
            ? current
            : (list[0]?.subjectId ?? null),
        );
      })
      .catch(() => {
        if (!cancelled) setFailed(true);
      });
    return () => {
      cancelled = true;
    };
  }, [childId, attempt]);

  useEffect(() => {
    if (subjectId === null) return;
    let cancelled = false;
    fetchChapters(childId, subjectId)
      .then((list) => {
        if (!cancelled) setChapters(list);
      })
      .catch(() => {
        if (!cancelled) setFailed(true);
      });
    return () => {
      cancelled = true;
    };
  }, [childId, subjectId, attempt]);

  const switchChild = useCallback(
    (id: string) => {
      if (id === childId) return;
      setChildId(id);
      setSubjects([]);
      setSubjectId(null);
      setChapters(null);
      setFailed(false);
      writeValue('activeChildId', id).catch(() => undefined);
    },
    [childId],
  );

  const selectSubject = useCallback(
    (id: string) => {
      if (id === subjectId) return;
      setSubjectId(id);
      setChapters(null);
    },
    [subjectId],
  );

  const retry = useCallback(() => {
    setFailed(false);
    setChapters(null);
    setAttempt((n) => n + 1);
  }, []);

  const state: ChaptersState = failed
    ? FAILED
    : chapters === null
      ? LOADING
      : { status: 'ready', chapters };

  return {
    activeChild: children.find((child) => child.id === childId) ?? null,
    children: children.map((child) => ({ ...child, stillBeingMade: false })),
    subjects,
    subjectId,
    state,
    switchChild,
    selectSubject,
    retry,
  };
}
