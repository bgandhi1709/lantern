import { useEffect, useMemo, useState } from 'react';
import { BackHandler } from 'react-native';

import { birthYearChoices, emptyChild, isChildComplete } from '../registration';
import type { Board } from '../registration';
import { makeChildId } from '../shared/crypto';
import { AddChildView } from './AddChildView';
import { useAddChild } from './useAddChild';

type Props = { board: Board; onAdded: () => void; onBack: () => void };

export function AddChildScreen({ board, onAdded, onBack }: Props) {
  const { adding, failure, add, isBusy, clearFailure } = useAddChild(onAdded);
  const [child, setChild] = useState(emptyChild);
  const [childId] = useState(makeChildId);
  const now = useMemo(() => new Date(), []);

  const goBack = () => (isBusy() ? undefined : onBack());

  useEffect(() => {
    const press = BackHandler.addEventListener('hardwareBackPress', () => {
      goBack();
      return true;
    });
    return () => press.remove();
  });

  return (
    <AddChildView
      board={board}
      child={child}
      years={birthYearChoices(now)}
      canAdd={isChildComplete(child, board, now)}
      adding={adding}
      failure={failure}
      onChange={(changes) => {
        clearFailure();
        setChild({ ...child, ...changes });
      }}
      onAdd={() => add({ childId, draft: child })}
      onBack={goBack}
    />
  );
}
