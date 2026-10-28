import { useSubmit } from '../shared/submit';
import { addChild } from './addChild';

export function useAddChild(onAdded: () => void) {
  const { submitting, submit, ...rest } = useSubmit(addChild, onAdded);
  return { adding: submitting, add: submit, ...rest };
}
