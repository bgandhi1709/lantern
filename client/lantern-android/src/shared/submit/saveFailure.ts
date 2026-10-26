import { ApiError } from '../api';

export type SaveFailure = 'classNotAvailable' | 'childLimitReached' | 'unreachable' | 'other';

export function failureOf(error: unknown): SaveFailure {
  if (!(error instanceof ApiError)) return 'other';
  if (error.code === 'class-not-available') return 'classNotAvailable';
  if (error.code === 'child-limit-reached') return 'childLimitReached';
  if (error.code === 'network' || error.code === 'timeout') return 'unreachable';
  return 'other';
}
