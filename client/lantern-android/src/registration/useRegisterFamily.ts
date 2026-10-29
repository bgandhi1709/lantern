import { useMarkRegistered } from '../auth';
import { trackRegistrationStep } from '../shared/analytics';
import { useSubmit } from '../shared/submit';
import { registerFamily } from './registerFamily';

export function useRegisterFamily() {
  const markRegistered = useMarkRegistered();
  const { submitting, submit, ...rest } = useSubmit(registerFamily, () => {
    trackRegistrationStep('first-child', 'finished');
    markRegistered();
  });
  return { finishing: submitting, finish: submit, ...rest };
}
