import { BoardView } from './BoardView';
import { useRegistrationDraft } from './RegistrationDraft';
import { useRegistrationStep } from './useRegistrationStep';

type Props = { onContinue: () => void; onBack: () => void };

export function BoardScreen({ onContinue, onBack }: Props) {
  const { draft, update } = useRegistrationDraft();
  const finishStep = useRegistrationStep('board');

  return (
    <BoardView
      board={draft.board}
      onSelect={(board) => update({ board })}
      onContinue={finishStep('board-continue', onContinue)}
      onBack={onBack}
    />
  );
}
