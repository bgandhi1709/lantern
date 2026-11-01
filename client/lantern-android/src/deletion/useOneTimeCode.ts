import { useRef, useState } from 'react';

import { useIsMounted } from '../shared/lifecycle/useIsMounted';
import { KEEPS_PROOF, deleteFailureOf } from './deleteFailure';
import type { DeleteFailure } from './deleteFailure';
import { requestCode, verifyCode } from './oneTimeCodeApi';
import type { OneTimeCodeOperation } from './oneTimeCodeApi';

export type Work = 'requesting' | 'verifying' | 'acting';

export const CODE_LENGTH = 6;
const digitsOf = (text: string) => text.replace(/\D/g, '').slice(0, CODE_LENGTH);

export function useOneTimeCode(operation: OneTimeCodeOperation) {
  const mounted = useIsMounted();
  const busy = useRef(false);
  const latestCode = useRef('');
  const challenge = useRef<string | null>(null);
  const [issued, setIssued] = useState(false);
  const [code, setCode] = useState('');
  const [proof, setProof] = useState<string | null>(null);
  const [work, setWork] = useState<Work | null>(null);
  const [failure, setFailure] = useState<DeleteFailure | null>(null);
  const [resent, setResent] = useState(false);

  const clearCode = () => {
    latestCode.current = '';
    setCode('');
    setProof(null);
  };

  const run = async (kind: Work, action: () => Promise<void>) => {
    if (busy.current) return;
    busy.current = true;
    setWork(kind);
    setFailure(null);
    try {
      await action();
    } catch (error) {
      if (!mounted.current) return;
      const reason = deleteFailureOf(error);
      setFailure(reason);
      if (!KEEPS_PROOF.includes(reason)) setProof(null);
    } finally {
      busy.current = false;
      if (mounted.current) setWork(null);
    }
  };

  const verify = (digits: string) =>
    challenge.current &&
    run('verifying', async () => {
      const received = await verifyCode(challenge.current ?? '', digits);
      if (mounted.current && latestCode.current === digits) setProof(received);
    });

  return {
    issued,
    code,
    proof,
    work,
    failure,
    resent,
    run,
    isBusy: () => busy.current,
    clearFailure: () => setFailure(null),
    request: (target: string, again: boolean) =>
      run('requesting', async () => {
        if (again) {
          clearCode();
          setResent(false);
        }
        const received = await requestCode(operation, target);
        if (!mounted.current) return;
        challenge.current = received;
        clearCode();
        setResent(again);
        setIssued(true);
      }),
    typeCode: (text: string) => {
      const digits = digitsOf(text);
      latestCode.current = digits;
      setCode(digits);
      setProof(null);
      setFailure(null);
      if (digits.length === CODE_LENGTH) verify(digits);
    },
    forget: () => {
      challenge.current = null;
      setIssued(false);
      clearCode();
      setFailure(null);
    },
  };
}
