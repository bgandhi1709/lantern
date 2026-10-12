import { createContext, useContext, useState } from 'react';
import type { ReactNode } from 'react';

import { getParent } from '../shared/session';
import type { Parent } from '../shared/session';
import type { Board } from './board';

// What the Parent has entered so far. It lives in memory only: closing the app or signing out drops
// it, and nothing is stored on the device until Finish succeeds.
export type RegistrationDraft = {
  parent: Parent;
  region: string;
  noticeAccepted: boolean;
  // null until the Parent picks one: a Family has no default Board.
  board: Board | null;
};

type DraftContext = {
  draft: RegistrationDraft;
  update: (changes: Partial<RegistrationDraft>) => void;
};

const Context = createContext<DraftContext | null>(null);

export function RegistrationDraftProvider({ children }: { children: ReactNode }) {
  const [draft, setDraft] = useState<RegistrationDraft>(() => ({
    parent: getParent(),
    region: '',
    noticeAccepted: false,
    board: null,
  }));
  const update = (changes: Partial<RegistrationDraft>) =>
    setDraft((current) => ({ ...current, ...changes }));

  return <Context.Provider value={{ draft, update }}>{children}</Context.Provider>;
}

export function useRegistrationDraft() {
  const context = useContext(Context);
  if (context === null) throw new Error('useRegistrationDraft needs a RegistrationDraftProvider.');
  return context;
}
