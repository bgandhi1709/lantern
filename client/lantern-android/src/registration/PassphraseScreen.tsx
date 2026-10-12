import { useState } from 'react';

import { createFamilyKeys, makeFamilyId } from '../shared/crypto';
import { canCreatePassphrase, isMismatch } from './passphrase';
import { PassphraseView } from './PassphraseView';
import { useRegistrationDraft } from './RegistrationDraft';

type Props = { onCreated: () => void; onBack: () => void };

// The Passphrase lives in this screen's state only: it is never put in the draft, so leaving the
// step drops it.
export function PassphraseScreen({ onCreated, onBack }: Props) {
  const { update } = useRegistrationDraft();
  const [passphrase, setPassphrase] = useState('');
  const [again, setAgain] = useState('');
  const [understood, setUnderstood] = useState(false);
  const [creating, setCreating] = useState(false);
  const [failed, setFailed] = useState(false);

  const create = async () => {
    setCreating(true);
    setFailed(false);
    try {
      const id = makeFamilyId();
      const keys = await createFamilyKeys(id, passphrase);
      update({ family: { id, keys } });
      onCreated();
    } catch {
      setFailed(true);
      setCreating(false);
    }
  };

  return (
    <PassphraseView
      passphrase={passphrase}
      again={again}
      understood={understood}
      mismatch={isMismatch(passphrase, again)}
      canCreate={canCreatePassphrase(passphrase, again, understood)}
      creating={creating}
      failed={failed}
      onPassphraseChange={setPassphrase}
      onAgainChange={setAgain}
      onUnderstoodToggle={() => setUnderstood((value) => !value)}
      onCreate={create}
      onBack={onBack}
    />
  );
}
