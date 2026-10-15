import { ApiError } from '../shared/api';

export type RegisterFailure = 'classNotAvailable' | 'unreachable' | 'other';

// One place turns what went wrong into the words the Parent reads (they live in en.ts).
export function failureOf(error: unknown): RegisterFailure {
  if (!(error instanceof ApiError)) return 'other';
  if (error.code === 'class-not-available') return 'classNotAvailable';
  if (error.code === 'network' || error.code === 'timeout') return 'unreachable';
  return 'other';
}
