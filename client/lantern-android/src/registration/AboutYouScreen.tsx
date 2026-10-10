import { signOutOfLantern } from '../auth';
import { AboutYouView } from './AboutYouView';
import { isValidRegion } from './region';
import { useRegistrationDraft } from './RegistrationDraft';

export function AboutYouScreen({ onContinue }: { onContinue: () => void }) {
  const { draft, update } = useRegistrationDraft();

  return (
    <AboutYouView
      parent={draft.parent}
      region={draft.region}
      noticeAccepted={draft.noticeAccepted}
      canContinue={isValidRegion(draft.region) && draft.noticeAccepted}
      onRegionChange={(region) => update({ region })}
      onNoticeToggle={() => update({ noticeAccepted: !draft.noticeAccepted })}
      onContinue={onContinue}
      onBack={signOutOfLantern}
    />
  );
}
