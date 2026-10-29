import { signOutOfLantern } from '../auth';
import { tapped } from '../shared/analytics';
import { AboutYouView } from './AboutYouView';
import { isValidRegion } from './region';
import { useRegistrationDraft } from './RegistrationDraft';
import { useRegistrationStep } from './useRegistrationStep';

type Props = { onOpenNotice: () => void; onContinue: () => void };

export function AboutYouScreen({ onOpenNotice, onContinue }: Props) {
  const { draft, update } = useRegistrationDraft();
  const finishStep = useRegistrationStep('about-you');

  return (
    <AboutYouView
      parent={draft.parent}
      region={draft.region}
      noticeAccepted={draft.noticeAccepted}
      canContinue={isValidRegion(draft.region) && draft.noticeAccepted}
      onRegionChange={(region) => update({ region })}
      onNoticeToggle={() => update({ noticeAccepted: !draft.noticeAccepted })}
      onOpenNotice={onOpenNotice}
      onContinue={finishStep('about-continue', onContinue)}
      onBack={signOutOfLantern}
      onChangeAccount={tapped('switch-google-account', signOutOfLantern)}
    />
  );
}
