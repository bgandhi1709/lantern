import { useEffect, useMemo, useRef, useState } from 'react';

import { useMarkRegistered } from '../auth';
import {
  blockingExtra,
  birthYearChoices,
  emptyChild,
  isChildComplete,
  isChildTouched,
} from './child';
import type { ChildDraft } from './child';
import { FirstChildView } from './FirstChildView';
import { registerFamily } from './registerFamily';
import { failureOf } from './registrationError';
import type { RegisterFailure } from './registrationError';
import { useRegistrationDraft } from './RegistrationDraft';

// The year range is fixed for the visit: a Parent who is still here at midnight on New Year keeps the list.
export function FirstChildScreen({ onBack }: { onBack: () => void }) {
  const { draft, update } = useRegistrationDraft();
  const markRegistered = useMarkRegistered();
  const [openIndex, setOpenIndex] = useState<number | null>(null);
  const [finishing, setFinishing] = useState(false);
  const [failure, setFailure] = useState<RegisterFailure | null>(null);
  const now = useMemo(() => new Date(), []);
  const busy = useRef(false);
  const mounted = useRef(true);
  useEffect(
    () => () => {
      mounted.current = false;
    },
    [],
  );

  const { board, family } = draft;
  if (board === null || family === null)
    throw new Error('The First child step needs a Board and the Family keys.');

  const drafts = draft.children;
  const extras = drafts.slice(1);
  const extraBlocks = blockingExtra(extras, board, now) !== null;
  const canFinish = isChildComplete(drafts[0], board, now) && !extraBlocks;

  const setDrafts = (next: ChildDraft[]) => update({ children: next });
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

  // One Finish at a time, and a result that arrives after the Parent left the step is dropped.
  const finish = async () => {
    if (busy.current || !canFinish) return;
    busy.current = true;
    setFinishing(true);
    setFailure(null);
    try {
      await registerFamily({
        family,
        parent: draft.parent,
        region: draft.region,
        board,
        children: drafts.filter((child, index) => index === 0 || isChildTouched(child)),
      });
      if (!mounted.current) return;
      markRegistered();
    } catch (error) {
      if (!mounted.current) return;
      setFailure(failureOf(error));
      setFinishing(false);
    } finally {
      busy.current = false;
    }
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
      onFinish={finish}
      onBack={() => (busy.current ? undefined : onBack())}
    />
  );
}
