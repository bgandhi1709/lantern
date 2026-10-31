import { useRef, useState } from 'react';

import { useIsMounted } from '../shared/lifecycle/useIsMounted';
import { requestDeleteCode, verifyDeleteCode } from './deleteCodesApi';
import { deleteChild } from './deleteChild';
import { KEEPS_PROOF, deleteFailureOf } from './deleteFailure';
import type { DeleteFailure } from './deleteFailure';
import { useDeletableFamily } from './useDeletableFamily';

export type Step = 'confirm' | 'code';
export type Work = 'requesting' | 'verifying' | 'deleting';

const CODE_LENGTH = 6;
const digitsOf = (text: string) => text.replace(/\D/g, '').slice(0, CODE_LENGTH);

export function useDeleteChild(onDeleted: () => void) {
  const family = useDeletableFamily();
  const mounted = useIsMounted();
  const busy = useRef(false);
  const latestCode = useRef('');
  const [step, setStep] = useState<Step>('confirm');
  const [pickedId, setPickedId] = useState<string | null>(null);
  const [typedName, setTypedName] = useState('');
  const [code, setCode] = useState('');
  const [proof, setProof] = useState<string | null>(null);
  const [work, setWork] = useState<Work | null>(null);
  const [failure, setFailure] = useState<DeleteFailure | null>(null);
  const [resent, setResent] = useState(false);

  const children = family.status === 'ready' ? family.children : [];
  const picked = children.find((child) => child.id === pickedId) ?? null;

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
    run('verifying', async () => {
      const received = await verifyDeleteCode(digits);
      if (mounted.current && latestCode.current === digits) setProof(received);
    });

  const emailCode = (again: boolean) =>
    picked &&
    run('requesting', async () => {
      await requestDeleteCode({ scope: 'child', childId: picked.id });
      if (!mounted.current) return;
      latestCode.current = '';
      setCode('');
      setProof(null);
      setResent(again);
      setStep('code');
    });

  return {
    family,
    step,
    picked,
    typedName,
    nameMatches: picked !== null && typedName === picked.name,
    code,
    verified: proof !== null,
    work,
    failure,
    resent,
    isBusy: () => busy.current,
    pick: (childId: string) => {
      setPickedId(childId);
      setTypedName('');
      setFailure(null);
    },
    typeName: (text: string) => {
      setTypedName(text);
      setFailure(null);
    },
    typeCode: (text: string) => {
      const digits = digitsOf(text);
      latestCode.current = digits;
      setCode(digits);
      setProof(null);
      setFailure(null);
      if (digits.length === CODE_LENGTH) verify(digits);
    },
    emailCode,
    backToName: () => {
      setStep('confirm');
      setFailure(null);
    },
    remove: () =>
      picked &&
      proof &&
      run('deleting', async () => {
        const otherChildIds = children.filter((child) => child.id !== picked.id).map((c) => c.id);
        await deleteChild({ childId: picked.id, proof, otherChildIds });
        if (mounted.current) onDeleted();
      }),
  };
}
