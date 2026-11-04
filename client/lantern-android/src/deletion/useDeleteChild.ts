import { useState } from 'react';

import { useIsMounted } from '../shared/lifecycle/useIsMounted';
import { deleteChild } from './deleteChild';
import { childrenOf, useDeletableFamily } from './useDeletableFamily';
import type { OneTimeCodeOperation } from './oneTimeCodeApi';
import { codeStepOf, useOneTimeCode } from './useOneTimeCode';

const OPERATION: OneTimeCodeOperation = 'delete-child';

export type { Work } from './useOneTimeCode';

export function useDeleteChild(onDeleted: () => void) {
  const family = useDeletableFamily();
  const mounted = useIsMounted();
  const oneTimeCode = useOneTimeCode(OPERATION);
  const [pickedId, setPickedId] = useState<string | null>(null);
  const [typedName, setTypedName] = useState('');

  const children = childrenOf(family);
  const picked = children.find((child) => child.id === pickedId) ?? null;
  const { proof, run, request, clearFailure } = oneTimeCode;

  return {
    family,
    picked,
    typedName,
    ...codeStepOf(oneTimeCode),
    nameMatches: picked !== null && typedName === picked.name,
    emailCode: (again: boolean) => picked && request(picked.id, again),
    pick: (childId: string) => {
      setPickedId(childId);
      setTypedName('');
      clearFailure();
    },
    typeName: (text: string) => {
      setTypedName(text);
      clearFailure();
    },
    remove: () =>
      picked &&
      proof &&
      run('acting', async () => {
        const otherChildIds = children.filter((child) => child.id !== picked.id).map((c) => c.id);
        await deleteChild({ childId: picked.id, proof, otherChildIds });
        if (mounted.current) onDeleted();
      }),
  };
}
