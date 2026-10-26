import { ChildFields, ChildFormFooter, ChildStepLayout, MAX_CHILDREN } from '../registration';
import type { Board, ChildDraft } from '../registration';
import { useStrings } from '../shared/i18n';
import type { SaveFailure } from '../shared/submit';
import { BackButton } from '../shared/ui/BackButton';

type Props = {
  board: Board;
  child: ChildDraft;
  years: string[];
  canAdd: boolean;
  adding: boolean;
  failure: SaveFailure | null;
  onChange: (changes: Partial<ChildDraft>) => void;
  onAdd: () => void;
  onBack: () => void;
};

export function AddChildView({
  board,
  child,
  years,
  canAdd,
  adding,
  failure,
  onChange,
  onAdd,
  onBack,
}: Props) {
  const strings = useStrings().addChild;

  return (
    <ChildStepLayout
      header={<BackButton onBack={onBack} />}
      heading={strings.heading}
      intro={strings.note(MAX_CHILDREN)}
      workingLabel={adding ? strings.working : null}
      footer={
        <ChildFormFooter
          label={adding ? strings.submitting : failure ? strings.tryAgain : strings.submit}
          disabled={!canAdd || adding}
          blockedText={null}
          failureText={failure ? strings.failures[failure] : null}
          onSubmit={onAdd}
        />
      }
    >
      <ChildFields idPrefix="child" child={child} board={board} years={years} onChange={onChange} />
    </ChildStepLayout>
  );
}
