import { signOutOfLantern } from '../auth';
import { AboutYouView } from './AboutYouView';
import { isValidRegion } from './region';
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
      onContinue={onContinue}
      onBack={signOutOfLantern}
      onChangeAccount={signOutOfLantern}
    />
  );
}
