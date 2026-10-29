import { useTrackScreen } from '../shared/analytics';
import { StartAgainView } from './StartAgainView';
import { useStartAgain } from './useStartAgain';

type Props = { onDeleted: () => void; onKeep: () => void };

export function StartAgainScreen({ onDeleted, onKeep }: Props) {
  useTrackScreen('start-again');
  const again = useStartAgain(onDeleted);

  return (
    <StartAgainView
      deleting={again.deleting}
      failed={again.failed}
      onDelete={again.remove}
      onKeep={() => (again.busy.current ? undefined : onKeep())}
    />
  );
}
