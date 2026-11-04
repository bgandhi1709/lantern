import { StartAgainView } from './StartAgainView';

type Props = { onContinue: () => void; onKeep: () => void };

export function StartAgainScreen({ onContinue, onKeep }: Props) {
  return <StartAgainView onDelete={onContinue} onKeep={onKeep} />;
}
