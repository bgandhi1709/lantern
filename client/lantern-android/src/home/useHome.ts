import { useCallback, useEffect, useRef, useState } from 'react';

import type { Board } from '../registration';
import { getParent } from '../shared/session';
import { readFamilyKey, readValue, writeValue } from '../shared/storage';
import type { Subject } from './homeApi';
import { subjectsOnceReady } from './subjectsOnceReady';
import { fetchUnlockedFamily } from './unlockedFamily';

export type HomeChild = { id: string; name: string; classLevel: number };

export type SwitcherChild = HomeChild & { stillBeingMade: boolean };

export type SubjectsState =
  { status: 'loading' } | { status: 'failed' } | { status: 'ready'; items: Subject[] };

const LOADING: SubjectsState = { status: 'loading' };
const FAILED: SubjectsState = { status: 'failed' };
const ready = (items: Subject[]): SubjectsState => ({ status: 'ready', items });

export function useHome() {
  const [children, setChildren] = useState<HomeChild[]>([]);
  const [board, setBoard] = useState<Board | null>(null);
  const [activeId, setActiveId] = useState<string | null>(null);
  const [subjectsByChild, setSubjectsByChild] = useState<Record<string, SubjectsState>>({});
  const [locked, setLocked] = useState(false);
  const [listFailed, setListFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const loadSubjectsRef = useRef<(child: HomeChild) => Promise<void>>(async () => undefined);

  useEffect(() => {
    let cancelled = false;
    const isCancelled = () => cancelled;

    const loadSubjects = async (child: HomeChild) => {
      const state = await subjectsOnceReady(child.id, isCancelled).then(ready, () => FAILED);
      if (!cancelled) setSubjectsByChild((current) => ({ ...current, [child.id]: state }));
    };

    loadSubjectsRef.current = loadSubjects;

    const load = async () => {
      const familyKey = await readFamilyKey(getParent().uid);
      if (!familyKey) {
        if (!cancelled) setLocked(true);
        return;
      }
      const [family, storedId] = await Promise.all([
        fetchUnlockedFamily(familyKey),
        readValue('activeChildId'),
      ]);
      if (cancelled) return;
      const list = family.children;
      const active = list.find((child) => child.id === storedId) ?? list[0];
      setBoard(family.board);
      setChildren(list);
      setActiveId(active?.id ?? null);
      if (active && active.id !== storedId) {
        writeValue('activeChildId', active.id).catch(() => undefined);
      }
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

  const reload = useCallback(() => {
    setChildren([]);
    setSubjectsByChild({});
    setLocked(false);
    setListFailed(false);
    setAttempt((n) => n + 1);
  }, []);

  const retry = useCallback(() => {
    const failed = children.filter((child) => subjectsByChild[child.id]?.status === 'failed');
    if (listFailed || locked || failed.length === 0) {
      reload();
      return;
    }
    setSubjectsByChild((current) => ({
      ...current,
      ...Object.fromEntries(failed.map((child) => [child.id, LOADING])),
    }));
    failed.forEach((child) => loadSubjectsRef.current(child));
  }, [children, subjectsByChild, listFailed, locked, reload]);

  const activeSubjects = activeId === null ? LOADING : (subjectsByChild[activeId] ?? LOADING);
  const subjects = listFailed ? FAILED : activeSubjects;

  return {
    locked,
    board,
    activeChild: children.find((child) => child.id === activeId) ?? null,
    children: children.map((child) => ({
      ...child,
      stillBeingMade: (subjectsByChild[child.id] ?? LOADING).status === 'loading',
    })),
    subjects,
    switchTo,
    retry,
    reload,
  };
}
