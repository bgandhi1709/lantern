import { useEffect, useRef, useState } from 'react';

import { trackTap } from '../shared/analytics';
import { createFamilyKeys, makeFamilyId } from '../shared/crypto';
import { useStrings } from '../shared/i18n';
import { useRegistrationDraft } from './RegistrationDraft';
import { RecoveryCodeView } from './RecoveryCodeView';
import { useRegistrationStep } from './useRegistrationStep';
import { emailRecoveryCode, shareRecoveryCode } from './shareRecoveryCode';

type Props = { onContinue: () => void; onBack: () => void };

export function RecoveryCodeScreen({ onContinue, onBack }: Props) {
  const strings = useStrings().registration.recovery;
  const { draft, update } = useRegistrationDraft();
  const finishStep = useRegistrationStep('recovery-code');
  const [saved, setSaved] = useState(draft.codeSent);
  const [emailed, setEmailed] = useState(draft.codeSent);
  const [shareFailed, setShareFailed] = useState(false);
  const [makeFailed, setMakeFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const making = useRef(false);
  const mounted = useRef(true);
  useEffect(
    () => () => {
      mounted.current = false;
    },
    [],
  );

  const hasFamily = draft.family !== null;
  useEffect(() => {
    if (hasFamily || making.current || makeFailed) return;
    making.current = true;
    const id = makeFamilyId();
    createFamilyKeys(id)
      .then((keys) => mounted.current && update({ family: { id, keys } }))
      .catch(() => mounted.current && setMakeFailed(true))
      .finally(() => {
        making.current = false;
      });
  }, [hasFamily, makeFailed, attempt, update]);

  const code = draft.family?.keys.recoveryCode ?? null;
  const send = async (action: () => Promise<unknown>) => {
    setShareFailed(false);
    try {
      await action();
      return true;
    } catch {
      setShareFailed(true);
      return false;
    }
  };

  return (
    <RecoveryCodeView
      code={code}
      saved={saved}
      emailed={emailed}
      shareFailed={shareFailed}
      makeFailed={makeFailed}
      onMakeAgain={() => {
        setMakeFailed(false);
        setAttempt((n) => n + 1);
      }}
      onSavedToggle={() => setSaved((value) => !value)}
      onEmail={async () => {
        if (code === null) return;
        trackTap('share-recovery-code');
        const message = strings.message(code);
        const opened = await send(() =>
          emailRecoveryCode(draft.parent.email, strings.mailSubject, message),
        );
        if (opened) setEmailed(true);
      }}
      onSave={() => {
        if (code === null) return;
        trackTap('share-recovery-code');
        return send(() => shareRecoveryCode(strings.message(code)));
      }}
      onContinue={finishStep('recovery-continue', () => {
        update({ codeSent: true });
        onContinue();
      })}
      onBack={onBack}
    />
  );
}
