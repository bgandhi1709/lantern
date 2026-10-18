import { useCallback, useEffect, useState } from 'react';

import { ApiError } from '../shared/api';
import { unlockText } from '../shared/crypto';
import { readValue, writeValue } from '../shared/storage';
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

export type SwitcherChild = HomeChild & { ready: boolean };

export type SubjectsState =
  { status: 'loading' } | { status: 'failed' } | { status: 'ready'; items: Subject[] };

const LOADING: SubjectsState = { status: 'loading' };
const FAILED: SubjectsState = { status: 'failed' };
const ready = (items: Subject[]): SubjectsState => ({ status: 'ready', items });

export function useHome() {
  const [children, setChildren] = useState<HomeChild[]>([]);
  const [activeId, setActiveId] = useState<string | null>(null);
  const [subjectsByChild, setSubjectsByChild] = useState<Record<string, SubjectsState>>({});
  const [locked, setLocked] = useState(false);
  const [listFailed, setListFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let cancelled = false;
    const isCancelled = () => cancelled;

    const loadSubjects = async (child: HomeChild) => {
      const state = await subjectsOnceReady(child.id, isCancelled).then(ready, () => FAILED);
      if (!cancelled) setSubjectsByChild((current) => ({ ...current, [child.id]: state }));
    };

    const load = async () => {
      const familyKey = await readValue('familyKey');
      if (!familyKey) {
        if (!cancelled) setLocked(true);
        return;
      }
      const [rows, storedId] = await Promise.all([fetchChildren(), readValue('activeChildId')]);
      const list = rows.map((row) => ({
        id: row.childId,
        name: unlockText(familyKey, row.name),
        classLevel: row.classLevel,
      }));
      if (cancelled) return;
      setChildren(list);
      setActiveId((list.find((child) => child.id === storedId) ?? list[0])?.id ?? null);
      list.forEach(loadSubjects);
    };

    load().catch(() => {
      if (!cancelled) setListFailed(true);
    });
    return () => {
      cancelled = true;
    };
  }, [attempt]);

  const switchTo = useCallback((childId: string) => {
    setActiveId(childId);
    writeValue('activeChildId', childId).catch(() => undefined);
  }, []);

  const retry = useCallback(() => {
    setChildren([]);
    setSubjectsByChild({});
    setLocked(false);
    setListFailed(false);
    setAttempt((n) => n + 1);
  }, []);

  const activeSubjects = activeId === null ? LOADING : (subjectsByChild[activeId] ?? LOADING);
  const subjects = listFailed ? FAILED : activeSubjects;

  return {
    locked,
    activeChild: children.find((child) => child.id === activeId) ?? null,
    children: children.map((child) => ({
      ...child,
      ready: subjectsByChild[child.id]?.status === 'ready',
    })),
    subjects,
    switchTo,
    retry,
  };
}
