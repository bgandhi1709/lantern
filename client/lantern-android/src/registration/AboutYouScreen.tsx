import { signOutOfLantern } from '../auth';
import { tapped } from '../shared/analytics';
import { AboutYouView } from './AboutYouView';
import { isValidRegion } from './region';
import { finishStep } from './finishStep';
import { useRegistrationDraft } from './RegistrationDraft';

type Props = { onOpenNotice: () => void; onContinue: () => void };

export function AboutYouScreen({ onOpenNotice, onContinue }: Props) {
  const { draft, update } = useRegistrationDraft();

  return (
    <AboutYouView
      parent={draft.parent}
      region={draft.region}
      noticeAccepted={draft.noticeAccepted}
      canContinue={isValidRegion(draft.region) && draft.noticeAccepted}
      onRegionChange={(region) => update({ region })}
      onNoticeToggle={() => update({ noticeAccepted: !draft.noticeAccepted })}
      onOpenNotice={onOpenNotice}
      onContinue={finishStep('about-you', 'about-continue', onContinue)}
      onBack={signOutOfLantern}
      onChangeAccount={tapped('switch-google-account', signOutOfLantern)}
    />
  );
}
