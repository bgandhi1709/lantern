import { AboutYouScreen } from './AboutYouScreen';
import { RegistrationDraftProvider } from './RegistrationDraft';

export function RegistrationFlow() {
  return (
    <RegistrationDraftProvider>
      <AboutYouScreen onContinue={() => undefined} />
    </RegistrationDraftProvider>
  );
}
