import { useState } from 'react';

import { useStrings } from '../shared/i18n';
import { useIsMounted } from '../shared/lifecycle/useIsMounted';
import { deleteFamily } from './deleteFamily';
import { codeStepOf, useOneTimeCode } from './useOneTimeCode';
import { useDeletableFamily } from './useDeletableFamily';

export function useDeleteFamily(onDeleted: () => void) {
  const confirmWord = useStrings().deleteFamily.confirmWord;
  const mounted = useIsMounted();
  const family = useDeletableFamily();
  const oneTimeCode = useOneTimeCode('delete-family');
  const [typed, setTyped] = useState('');
  const { proof, run, request, clearFailure } = oneTimeCode;

  return {
    family,
    typed,
    ...codeStepOf(oneTimeCode),
    wordMatches: typed === confirmWord && family.status !== 'loading',
    emailCode: (again: boolean) => request(undefined, again),
    typeWord: (text: string) => {
      setTyped(text);
      clearFailure();
    },
    remove: () =>
      proof &&
      run('acting', async () => {
        await deleteFamily(proof);
        if (mounted.current) onDeleted();
      }),
  };
}
