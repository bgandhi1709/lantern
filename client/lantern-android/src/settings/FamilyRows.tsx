import { useStrings } from '../shared/i18n';
import { FamilyRow } from './FamilyRow';

type Props = { onUnlock?: () => void; onStartAgain?: () => void };

export function FamilyRows({ onUnlock, onStartAgain }: Props) {
  const text = useStrings().appearance;

  return (
    <>
      {onUnlock ? <FamilyRow {...text.unlockRow} onPress={onUnlock} /> : null}
      {onStartAgain ? <FamilyRow {...text.startAgainRow} onPress={onStartAgain} /> : null}
    </>
  );
}
