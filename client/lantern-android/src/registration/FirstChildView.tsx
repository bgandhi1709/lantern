import { useStrings } from '../shared/i18n';
import type { SaveFailure } from '../shared/submit';
import type { Board } from './board';
import type { ChildDraft } from './child';
import { ChildFields } from './ChildFields';
import { ChildFormFooter } from './ChildFormFooter';
import { ChildStepLayout } from './ChildStepLayout';
import { MoreChildren } from './MoreChildren';
import { StepHeader } from './StepHeader';
import { REGISTRATION_STEP_COUNT } from './steps';

type Props = {
  board: Board;
  // The first Child, then the extras.
  drafts: ChildDraft[];
  years: string[];
  now: Date;
  // The extra whose card is open, as an index into `drafts`, or null.
  openIndex: number | null;
  canFinish: boolean;
  extraBlocks: boolean;
  finishing: boolean;
  failure: SaveFailure | null;
  onChange: (index: number, changes: Partial<ChildDraft>) => void;
  onToggle: (index: number) => void;
  onAdd: () => void;
  onRemove: (index: number) => void;
  onFinish: () => void;
  onBack: () => void;
};

export function FirstChildView({
  board,
  drafts,
  years,
  now,
  openIndex,
  canFinish,
  extraBlocks,
  finishing,
  failure,
  onChange,
  onToggle,
  onAdd,
  onRemove,
  onFinish,
  onBack,
}: Props) {
  const strings = useStrings().registration.firstChild;

  return (
    <ChildStepLayout
      header={<StepHeader step={4} total={REGISTRATION_STEP_COUNT} onBack={onBack} />}
      heading={strings.heading}
      intro={strings.intro}
      workingLabel={finishing ? strings.working : null}
      footer={
        <ChildFormFooter
          label={finishing ? strings.finishing : failure ? strings.tryAgain : strings.finish}
          disabled={!canFinish || finishing}
          blockedText={extraBlocks && openIndex === null ? strings.more.blocked : null}
          failureText={failure ? strings.failures[failure] : null}
          onSubmit={onFinish}
        />
      }
    >
      <ChildFields
        idPrefix="child-1"
        child={drafts[0]}
        board={board}
        years={years}
        onChange={(changes) => onChange(0, changes)}
      />
      <MoreChildren
        board={board}
        drafts={drafts}
        years={years}
        now={now}
        openIndex={openIndex}
        onChange={onChange}
        onToggle={onToggle}
        onAdd={onAdd}
        onRemove={onRemove}
      />
    </ChildStepLayout>
  );
}
