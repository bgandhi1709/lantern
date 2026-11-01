import { useEffect, useState } from 'react';

import { fetchUnlockedFamily } from '../home';
import type { HomeChild } from '../home';
import { getParent } from '../shared/session';
import { readFamilyKey } from '../shared/storage';

export type FamilyState =
  { status: 'loading' } | { status: 'failed' } | { status: 'ready'; children: HomeChild[] };

export const childrenOf = (family: FamilyState) =>
  family.status === 'ready' ? family.children : [];

export function useDeletableFamily() {
  const [family, setFamily] = useState<FamilyState>({ status: 'loading' });

  useEffect(() => {
    let cancelled = false;
    readFamilyKey(getParent().uid)
      .then((key) => {
        if (key === null) throw new Error('Deleting needs the Family key on this phone.');
        return fetchUnlockedFamily(key);
      })
      .then(
        ({ children }) => !cancelled && setFamily({ status: 'ready', children }),
        () => !cancelled && setFamily({ status: 'failed' }),
      );
    return () => {
      cancelled = true;
    };
  }, []);

  return family;
}
