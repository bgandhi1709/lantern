import { useState } from 'react';

import { deleteFamily } from './deleteFamily';
import { codeStepOf, useOneTimeCode } from './useOneTimeCode';
import { useDeletableFamily } from './useDeletableFamily';

export const CONFIRM_WORD = 'DELETE';

export function useDeleteFamily(onDeleted: () => void) {
  const family = useDeletableFamily();
  const oneTimeCode = useOneTimeCode('delete-family');
  const [typed, setTyped] = useState('');
  const { proof, run, request, clearFailure } = oneTimeCode;

  return {
    family,
    typed,
    ...codeStepOf(oneTimeCode),
    wordMatches: typed === CONFIRM_WORD && family.status !== 'loading',
    emailCode: (again: boolean) => request(undefined, again),
    typeWord: (text: string) => {
      setTyped(text);
      clearFailure();
    },
    remove: () =>
      proof &&
      run('acting', async () => {
        await deleteFamily(proof);
        onDeleted();
      }),
  };
}
