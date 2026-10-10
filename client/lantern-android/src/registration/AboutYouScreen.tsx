import { signOutOfLantern } from '../auth';
import { getParent } from '../shared/session';
import { AboutYouView } from './AboutYouView';

export function AboutYouScreen() {
  return <AboutYouView parent={getParent()} onBack={signOutOfLantern} />;
}
