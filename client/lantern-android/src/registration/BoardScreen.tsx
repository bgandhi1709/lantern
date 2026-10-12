import { BoardView } from './BoardView';
import { useRegistrationDraft } from './RegistrationDraft';

type Props = { onContinue: () => void; onBack: () => void };

export function BoardScreen({ onContinue, onBack }: Props) {
  const { draft, update } = useRegistrationDraft();

  return (
    <BoardView
      board={draft.board}
      onSelect={(board) => update({ board })}
      onContinue={onContinue}
      onBack={onBack}
    />
  );
}
