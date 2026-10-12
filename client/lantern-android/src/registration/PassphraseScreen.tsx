import { useEffect, useRef, useState } from 'react';

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
  const busy = useRef(false);
  const mounted = useRef(true);
  useEffect(
    () => () => {
      mounted.current = false;
    },
    [],
  );

  // One creation at a time, and a result that arrives after the Parent left the step is dropped.
  const create = async () => {
    if (busy.current) return;
    busy.current = true;
    setCreating(true);
    setFailed(false);
    try {
      const id = makeFamilyId();
      const keys = await createFamilyKeys(id, passphrase);
      if (!mounted.current) return;
      update({ family: { id, keys } });
      onCreated();
    } catch {
      if (!mounted.current) return;
      setFailed(true);
      setCreating(false);
    } finally {
      busy.current = false;
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
      onBack={() => (busy.current ? undefined : onBack())}
    />
  );
}
