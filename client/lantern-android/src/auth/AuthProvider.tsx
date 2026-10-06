import { createContext, useContext, useEffect, useState } from 'react';
import type { ReactNode } from 'react';

import { onSessionChange } from '../shared/session';

export type SessionState = 'restoring' | 'signedOut' | 'signedIn';

const SessionContext = createContext<SessionState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<SessionState>('restoring');

  useEffect(() => onSessionChange((signedIn) => setState(signedIn ? 'signedIn' : 'signedOut')), []);

  return <SessionContext.Provider value={state}>{children}</SessionContext.Provider>;
}

export function useSessionState() {
  const state = useContext(SessionContext);
  if (state === null) throw new Error('useSessionState needs an AuthProvider above it.');
  return state;
}
