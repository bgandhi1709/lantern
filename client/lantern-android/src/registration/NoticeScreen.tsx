import { useTrackScreen } from '../shared/analytics';
import { NoticeView } from './NoticeView';

type Props = { version: string; onBack: () => void };

export function NoticeScreen({ version, onBack }: Props) {
  useTrackScreen('privacy-notice');

  return <NoticeView version={version} onBack={onBack} />;
}
