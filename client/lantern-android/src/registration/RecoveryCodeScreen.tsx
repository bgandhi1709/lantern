import { useState } from 'react';

import { useStrings } from '../shared/i18n';
import { useRegistrationDraft } from './RegistrationDraft';
import { RecoveryCodeView } from './RecoveryCodeView';
import { emailRecoveryCode, shareRecoveryCode } from './shareRecoveryCode';

export function RecoveryCodeScreen({ onContinue }: { onContinue: () => void }) {
  const strings = useStrings().registration.recovery;
  const { draft } = useRegistrationDraft();
  const [saved, setSaved] = useState(false);
  if (draft.family === null) throw new Error('The Recovery code step needs the Family keys.');
  const message = strings.message(draft.family.keys.recoveryCode);

  return (
    <RecoveryCodeView
      code={draft.family.keys.recoveryCode}
      saved={saved}
      onSavedToggle={() => setSaved((value) => !value)}
      onEmail={() => emailRecoveryCode(draft.parent.email, strings.mailSubject, message)}
      onSave={() => shareRecoveryCode(message)}
      onContinue={onContinue}
    />
  );
}
