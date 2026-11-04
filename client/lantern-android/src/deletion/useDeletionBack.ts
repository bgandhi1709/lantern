import { useEffect } from 'react';
import { BackHandler } from 'react-native';

import type { codeStepOf } from './useOneTimeCode';

type Deletion = Pick<ReturnType<typeof codeStepOf>, 'step' | 'isBusy' | 'backToName'>;

export function useDeletionBack(deletion: Deletion, onBack: () => void) {
  const goBack = () => {
    if (deletion.isBusy()) return;
    if (deletion.step === 'code') deletion.backToName();
    else onBack();
  };

  const cancel = () => (deletion.isBusy() ? undefined : onBack());

  useEffect(() => {
    const press = BackHandler.addEventListener('hardwareBackPress', () => {
      goBack();
      return true;
    });
    return () => press.remove();
  });

  return { goBack, cancel };
}
