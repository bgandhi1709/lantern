import { BoardView } from './BoardView';
import { finishStep } from './finishStep';
import { useRegistrationDraft } from './RegistrationDraft';

type Props = { onContinue: () => void; onBack: () => void };

export function BoardScreen({ onContinue, onBack }: Props) {
  const { draft, update } = useRegistrationDraft();

  return (
    <BoardView
      board={draft.board}
      onSelect={(board) => update({ board })}
      onContinue={finishStep('board', 'board-continue', onContinue)}
      onBack={onBack}
    />
  );
}
