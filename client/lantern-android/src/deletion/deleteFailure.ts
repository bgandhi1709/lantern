import { ApiError } from '../shared/api';

export type DeleteFailure =
  | 'wrongCode'
  | 'codeExpired'
  | 'tooManyTries'
  | 'tooSoon'
  | 'emailNotVerified'
  | 'emailNotSent'
  | 'unreachable'
  | 'other';

const BY_CODE: Record<string, DeleteFailure> = {
  'code-wrong': 'wrongCode',
  'code-expired': 'codeExpired',
  'proof-required': 'codeExpired',
  'code-attempts-used': 'tooManyTries',
  'code-too-soon': 'tooSoon',
  'email-not-verified': 'emailNotVerified',
  'email-not-sent': 'emailNotSent',
  network: 'unreachable',
  timeout: 'unreachable',
};

export function deleteFailureOf(error: unknown): DeleteFailure {
  return (error instanceof ApiError && BY_CODE[error.code]) || 'other';
}

export const KEEPS_PROOF: readonly DeleteFailure[] = ['unreachable', 'other'];
