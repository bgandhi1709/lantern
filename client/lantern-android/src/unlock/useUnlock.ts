import { useCallback, useEffect, useRef, useState } from 'react';

import { LockError } from '../shared/crypto';
import { getParent } from '../shared/session';
import { readValue } from '../shared/storage';
import type { FamilyLock, UnlockKind } from './unlockFamily';
import { UnlockWaitError, fetchFamilyLock, unlockFamily } from './unlockFamily';
import type { UnlockPhase } from './UnlockView';
import { useIsMounted } from './useIsMounted';

const TICK_MS = 1000;
const MS_PER_SECOND = 1000;

// The secrets are typed into this state only. They are never stored, logged or sent.
export function useUnlock(onOpened: () => void) {
  const [phase, setPhase] = useState<UnlockPhase>('loading');
  const [kind, setKind] = useState<UnlockKind>('passphrase');
  const [value, setValue] = useState('');
  const [opening, setOpening] = useState(false);
  const [wrong, setWrong] = useState(false);
  const [somethingWentWrong, setSomethingWentWrong] = useState(false);
  const [secondsLeft, setSecondsLeft] = useState(0);
  const lock = useRef<FamilyLock | null>(null);
  const busy = useRef(false);
  const mounted = useIsMounted();

  const fetchLock = useCallback(() => {
    fetchFamilyLock().then(
      (found) => {
        lock.current = found;
        if (mounted.current) setPhase('ready');
      },
      () => mounted.current && setPhase('failed'),
    );
  }, [mounted]);

  useEffect(fetchLock, [fetchLock]);

  const showWait = useCallback(
    (until: number) => setSecondsLeft(Math.max(0, Math.ceil((until - Date.now()) / MS_PER_SECOND))),
    [],
  );

  useEffect(() => {
    readValue('unlockWait').then(({ until }) => mounted.current && showWait(until));
  }, [mounted, showWait]);

  useEffect(() => {
    if (secondsLeft <= 0) return;
    const timer = setTimeout(() => setSecondsLeft((left) => left - 1), TICK_MS);
    return () => clearTimeout(timer);
  }, [secondsLeft]);

  const open = async () => {
    if (busy.current || lock.current === null) return;
    busy.current = true;
    setOpening(true);
    setWrong(false);
    setSomethingWentWrong(false);
    try {
      await unlockFamily(lock.current, { uid: getParent().uid, kind, typed: value });
      if (mounted.current) onOpened();
    } catch (error) {
      if (!mounted.current) return;
      if (error instanceof LockError) setWrong(true);
      else if (!(error instanceof UnlockWaitError)) setSomethingWentWrong(true);
      showWait((await readValue('unlockWait')).until);
      setOpening(false);
    } finally {
      busy.current = false;
    }
  };

  return {
    phase,
    kind,
    value,
    opening,
    wrong,
    somethingWentWrong,
    secondsLeft,
    busy,
    pickKind: (next: UnlockKind) => {
      setKind(next);
      setValue('');
      setWrong(false);
    },
    change: (text: string) => {
      setValue(text);
      setWrong(false);
    },
    open,
    loadAgain: () => {
      setPhase('loading');
      fetchLock();
    },
  };
}
