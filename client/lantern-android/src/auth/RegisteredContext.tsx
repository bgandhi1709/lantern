import { createContext, useContext } from 'react';

const Context = createContext<(() => void) | null>(null);

// Registration calls this when the API has made the Family, so Home shows at once instead of the
// app asking /v1/me again.
export const RegisteredProvider = Context.Provider;

export function useMarkRegistered() {
  const markRegistered = useContext(Context);
  if (markRegistered === null) throw new Error('useMarkRegistered needs a RegisteredProvider.');
  return markRegistered;
}
