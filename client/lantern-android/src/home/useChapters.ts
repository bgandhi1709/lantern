import { useCallback, useEffect, useState } from 'react';

import { useDebouncedValue } from '../shared/paging/useDebouncedValue';
import { usePagedList } from '../shared/paging/usePagedList';
import { getParent } from '../shared/session';
import { readFamilyKey, writeValue } from '../shared/storage';
import { fetchChaptersPage } from './chaptersApi';
import { fetchSubjects } from './homeApi';
import type { Subject } from './homeApi';
import { fetchUnlockedChildren } from './unlockedChildren';
import type { HomeChild } from './useHome';

const SEARCH_DELAY_MS = 300;

export function useChapters(initialChildId: string, initialSubjectId: string) {
  const [childId, setChildId] = useState(initialChildId);
  const [subjectId, setSubjectId] = useState<string | null>(initialSubjectId);
  const [children, setChildren] = useState<HomeChild[]>([]);
  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [query, setQuery] = useState('');
  const [failed, setFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);

  const typed = query.trim();
  const settled = useDebouncedValue(typed, SEARCH_DELAY_MS);
  const search = typed === '' ? '' : settled;
  const list = usePagedList({
    key: `${childId}/${subjectId}/${search}`,
    group: `${childId}/${subjectId}`,
    enabled: subjectId !== null,
    load: (cursor) => fetchChaptersPage(childId, subjectId ?? '', search, cursor),
  });
  const reloadList = list.reload;

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      const familyKey = await readFamilyKey(getParent().uid);
      if (!familyKey) throw new Error('This phone does not hold the Family key.');
      const loaded = await fetchUnlockedChildren(familyKey);
      if (!cancelled) setChildren(loaded);
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
      .then((loaded) => {
        if (cancelled) return;
        setSubjects(loaded);
        setSubjectId((current) =>
          loaded.some((subject) => subject.subjectId === current)
            ? current
            : (loaded[0]?.subjectId ?? null),
        );
      })
      .catch(() => {
        if (!cancelled) setFailed(true);
      });
    return () => {
      cancelled = true;
    };
  }, [childId, attempt]);

  const switchChild = useCallback(
    (id: string) => {
      if (id === childId) return;
      setChildId(id);
      setSubjects([]);
      setSubjectId(null);
      setQuery('');
      setFailed(false);
      writeValue('activeChildId', id).catch(() => undefined);
    },
    [childId],
  );

  const selectSubject = useCallback(
    (id: string) => {
      if (id === subjectId) return;
      setSubjectId(id);
      setQuery('');
    },
    [subjectId],
  );

  const retry = useCallback(() => {
    setFailed(false);
    setAttempt((n) => n + 1);
    reloadList();
  }, [reloadList]);

  return {
    childId,
    activeChild: children.find((child) => child.id === childId) ?? null,
    children: children.map((child) => ({ ...child, stillBeingMade: false })),
    subjects,
    subjectId,
    query,
    setQuery,
    chapters: {
      status: failed ? ('failed' as const) : list.status,
      items: list.items,
      progress: list.meta,
      refreshing: list.refreshing,
      loadingMore: list.loadingMore,
      moreFailed: list.moreFailed,
    },
    loadMore: list.loadMore,
    retryMore: list.retryMore,
    switchChild,
    selectSubject,
    retry,
  };
}

export type ChaptersList = ReturnType<typeof useChapters>['chapters'];
