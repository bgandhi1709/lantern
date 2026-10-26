import { useEffect, useRef, useState } from 'react';

import { failureOf } from './saveFailure';
import type { SaveFailure } from './saveFailure';

export function useSubmit<Input>(run: (input: Input) => Promise<unknown>, onDone: () => void) {
  const [submitting, setSubmitting] = useState(false);
  const [failure, setFailure] = useState<SaveFailure | null>(null);
  const busy = useRef(false);
  const mounted = useRef(true);
  useEffect(
    () => () => {
      mounted.current = false;
    },
    [],
  );

  const submit = async (input: Input) => {
    if (busy.current) return;
    busy.current = true;
    setSubmitting(true);
    setFailure(null);
    try {
      await run(input);
      if (!mounted.current) return;
      onDone();
    } catch (error) {
      if (!mounted.current) return;
      setFailure(failureOf(error));
      setSubmitting(false);
    } finally {
      busy.current = false;
    }
  };

  return {
    submitting,
    failure,
    submit,
    isBusy: () => busy.current,
    clearFailure: () => setFailure(null),
  };
}
