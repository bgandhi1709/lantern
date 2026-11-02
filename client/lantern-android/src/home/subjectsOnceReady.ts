import { ApiError } from '../shared/api';
import { fetchSubjects } from './homeApi';

export const CONTENT_POLL_MS = 3_000;
export const RATE_LIMITED_POLL_MS = 15_000;
export const MAX_CONTENT_POLLS = 40;
export const MAX_RATE_LIMITED_POLLS = 8;

const TOO_MANY_REQUESTS = 429;

const isNotReady = (error: unknown) =>
  error instanceof ApiError && error.code === 'content-not-ready';

const isRateLimited = (error: unknown) =>
  error instanceof ApiError && error.status === TOO_MANY_REQUESTS;

const wait = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));

export async function subjectsOnceReady(childId: string, isCancelled: () => boolean) {
  let notReady = 0;
  let rateLimited = 0;
  for (;;) {
    try {
      return await fetchSubjects(childId);
    } catch (error) {
      if (isCancelled()) throw error;
      if (isNotReady(error) && notReady++ < MAX_CONTENT_POLLS) await wait(CONTENT_POLL_MS);
      else if (isRateLimited(error) && rateLimited++ < MAX_RATE_LIMITED_POLLS) {
        await wait(RATE_LIMITED_POLL_MS);
      } else throw error;
      if (isCancelled()) return [];
    }
  }
}
