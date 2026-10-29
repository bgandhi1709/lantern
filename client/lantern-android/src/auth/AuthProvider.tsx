import { createContext, useContext, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';

import { setCollectionEnabled } from '../shared/analytics';
import { onSessionChange } from '../shared/session';

type RegistrationCheck = { epoch: number; recheck: () => void };

export type SessionState = 'restoring' | 'signedOut' | 'signedIn';

const SessionContext = createContext<SessionState | null>(null);
const RegistrationCheckContext = createContext<RegistrationCheck | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<SessionState>('restoring');
  const [epoch, setEpoch] = useState(0);
  const check = useMemo(() => ({ epoch, recheck: () => setEpoch((n) => n + 1) }), [epoch]);

  useEffect(() => onSessionChange((signedIn) => setState(signedIn ? 'signedIn' : 'signedOut')), []);

  useEffect(() => {
    if (state === 'signedOut') setCollectionEnabled(false);
  }, [state]);

  return (
    <SessionContext.Provider value={state}>
      <RegistrationCheckContext.Provider value={check}>
        {children}
      </RegistrationCheckContext.Provider>
    </SessionContext.Provider>
  );
}

export function useSessionState() {
  const state = useContext(SessionContext);
  if (state === null) throw new Error('useSessionState needs an AuthProvider above it.');
  return state;
}

export function useRegistrationCheck() {
  const check = useContext(RegistrationCheckContext);
  if (check === null) throw new Error('useRegistrationCheck needs an AuthProvider above it.');
  return check;
}
