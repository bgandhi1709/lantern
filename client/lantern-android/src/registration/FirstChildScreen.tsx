import { useEffect, useMemo, useState } from 'react';
import { BackHandler } from 'react-native';

import { trackTap } from '../shared/analytics';
import {
  blockingExtra,
  birthYearChoices,
  emptyChild,
  isChildComplete,
  isChildTouched,
} from './child';
import type { ChildDraft } from './child';
import { FirstChildView } from './FirstChildView';
import { useRegistrationDraft } from './RegistrationDraft';
import { useRegisterFamily } from './useRegisterFamily';
import { useRegistrationStep } from './useRegistrationStep';

// The year range is fixed for the visit: a Parent who is still here at midnight on New Year keeps the list.
export function FirstChildScreen({ onBack }: { onBack: () => void }) {
  const { draft, update } = useRegistrationDraft();
  useRegistrationStep('first-child');
  const { finishing, failure, finish, isBusy, clearFailure } = useRegisterFamily();
  const [openIndex, setOpenIndex] = useState<number | null>(null);
  const now = useMemo(() => new Date(), []);

  // Android's Back button is this step's too, and it does nothing while Finish runs.
  useEffect(() => {
    const press = BackHandler.addEventListener('hardwareBackPress', () => {
      if (!isBusy()) onBack();
      return true;
    });
    return () => press.remove();
  });

  const { board, family } = draft;
  if (board === null || family === null)
    throw new Error('The First child step needs a Board and the Family keys.');

  const drafts = draft.children;
  const extraBlocks = blockingExtra(drafts.slice(1), board, now) !== null;
  const canFinish = isChildComplete(drafts[0], board, now) && !extraBlocks;

  // Any edit clears the last failure, so the message never sits beside input the Parent has fixed.
  const setDrafts = (next: ChildDraft[]) => {
    clearFailure();
    update({ children: next });
  };
  const change = (index: number, changes: Partial<ChildDraft>) =>
    setDrafts(drafts.map((child, at) => (at === index ? { ...child, ...changes } : child)));
  const add = () => {
    setDrafts([...drafts, emptyChild()]);
    setOpenIndex(drafts.length);
  };
  const remove = (index: number) => {
    setDrafts(drafts.filter((_, at) => at !== index));
    setOpenIndex(null);
  };

  return (
    <FirstChildView
      board={board}
      drafts={drafts}
      years={birthYearChoices(now)}
      now={now}
      openIndex={openIndex}
      canFinish={canFinish}
      extraBlocks={extraBlocks}
      finishing={finishing}
      failure={failure}
      onChange={change}
      onToggle={(index) => setOpenIndex(openIndex === index ? null : index)}
      onAdd={add}
      onRemove={remove}
      onFinish={() => {
        trackTap('finish-registration');
        return finish({
          family,
          parent: draft.parent,
          region: draft.region,
          board,
          children: drafts.filter((child, index) => index === 0 || isChildTouched(child)),
        });
      }}
      onBack={() => (isBusy() ? undefined : onBack())}
    />
  );
}
