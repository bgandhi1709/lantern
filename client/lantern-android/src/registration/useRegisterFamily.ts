import { useMarkRegistered } from '../auth';
import { useSubmit } from '../shared/submit';
import { registerFamily } from './registerFamily';

export function useRegisterFamily() {
  const { submitting, submit, ...rest } = useSubmit(registerFamily, useMarkRegistered());
  return { finishing: submitting, finish: submit, ...rest };
}
