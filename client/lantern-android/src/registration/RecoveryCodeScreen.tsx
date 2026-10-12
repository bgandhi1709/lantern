import { useState } from 'react';

import { useStrings } from '../shared/i18n';
import { useRegistrationDraft } from './RegistrationDraft';
import { RecoveryCodeView } from './RecoveryCodeView';
import { emailRecoveryCode, shareRecoveryCode } from './shareRecoveryCode';

export function RecoveryCodeScreen({ onContinue }: { onContinue: () => void }) {
  const strings = useStrings().registration.recovery;
  const { draft } = useRegistrationDraft();
  const [saved, setSaved] = useState(false);
  const [shareFailed, setShareFailed] = useState(false);
  if (draft.family === null) throw new Error('The Recovery code step needs the Family keys.');
  const message = strings.message(draft.family.keys.recoveryCode);
  const send = async (action: () => Promise<unknown>) => {
    setShareFailed(false);
    try {
      await action();
    } catch {
      setShareFailed(true);
    }
  };

  return (
    <RecoveryCodeView
      code={draft.family.keys.recoveryCode}
      saved={saved}
      shareFailed={shareFailed}
      onSavedToggle={() => setSaved((value) => !value)}
      onEmail={() =>
        send(() => emailRecoveryCode(draft.parent.email, strings.mailSubject, message))
      }
      onSave={() => send(() => shareRecoveryCode(message))}
      onContinue={onContinue}
    />
  );
}
