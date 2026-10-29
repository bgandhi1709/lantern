import { useTrackScreen } from '../shared/analytics';
import { ComingNextView } from './ComingNextView';

export function ComingNextScreen({ onBack }: { onBack: () => void }) {
  useTrackScreen('coming-next');

  return <ComingNextView onBack={onBack} />;
}
