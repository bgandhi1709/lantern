import { ApiError } from '../shared/api';
import { fetchSubjects } from './homeApi';

export const CONTENT_POLL_MS = 3_000;
export const MAX_CONTENT_POLLS = 40;

const isNotReady = (error: unknown) =>
  error instanceof ApiError && error.code === 'content-not-ready';

const wait = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));

export async function subjectsOnceReady(childId: string, isCancelled: () => boolean) {
  for (let polls = 0; ; polls++) {
    try {
      return await fetchSubjects(childId);
    } catch (error) {
      if (!isNotReady(error) || isCancelled() || polls >= MAX_CONTENT_POLLS) throw error;
      await wait(CONTENT_POLL_MS);
      if (isCancelled()) return [];
    }
  }
}
