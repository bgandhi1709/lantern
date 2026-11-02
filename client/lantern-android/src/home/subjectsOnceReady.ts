import { ApiError } from '../shared/api';
import { fetchSubjects } from './homeApi';

export const CONTENT_POLL_MS = 3_000;
export const RATE_LIMITED_POLL_MS = 15_000;
export const MAX_CONTENT_POLLS = 40;

const TOO_MANY_REQUESTS = 429;

const waitBefore = (error: unknown) => {
  if (!(error instanceof ApiError)) return null;
  if (error.status === TOO_MANY_REQUESTS) return RATE_LIMITED_POLL_MS;
  return error.code === 'content-not-ready' ? CONTENT_POLL_MS : null;
};

const wait = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));

export async function subjectsOnceReady(childId: string, isCancelled: () => boolean) {
  for (let polls = 0; ; polls++) {
    try {
      return await fetchSubjects(childId);
    } catch (error) {
      const waitMs = waitBefore(error);
      if (waitMs === null || isCancelled() || polls >= MAX_CONTENT_POLLS) throw error;
      await wait(waitMs);
      if (isCancelled()) return [];
    }
  }
}
