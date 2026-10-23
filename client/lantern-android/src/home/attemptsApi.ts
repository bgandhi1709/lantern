import { z } from 'zod';

import { api } from '../shared/api';
import { CHAPTER_STATES } from './chaptersApi';

export const ATTEMPTS_PAGE_SIZE = 10;

const ChapterHistory = z.object({
  chapter: z.object({
    chapterId: z.string(),
    number: z.number().int(),
    title: z.string(),
    subjectId: z.string(),
    subjectName: z.string(),
    state: z.enum(CHAPTER_STATES),
    attemptCount: z.number().int(),
    checkCount: z.number().int(),
    conceptsDone: z.number().int().nullable(),
    conceptCount: z.number().int().nullable(),
  }),
  items: z.array(
    z.object({
      attemptNumber: z.number().int(),
      plannedAt: z.string(),
      state: z.enum(CHAPTER_STATES),
      conceptsDone: z.number().int(),
      conceptCount: z.number().int(),
      checks: z.array(
        z.object({
          checkId: z.string(),
          checkNumber: z.number().int(),
          takenAt: z.string(),
          right: z.number().int(),
          of: z.number().int(),
          wrong: z.array(z.object({ conceptId: z.string(), title: z.string() })),
        }),
      ),
    }),
  ),
  nextCursor: z.string().nullable(),
});

export type Attempt = z.infer<typeof ChapterHistory>['items'][number];
export type Check = Attempt['checks'][number];
export type ChapterHeader = z.infer<typeof ChapterHistory>['chapter'];

export async function fetchAttemptsPage(childId: string, chapterId: string, cursor: string | null) {
  const params = [`limit=${ATTEMPTS_PAGE_SIZE}`];
  if (cursor !== null) params.push(`cursor=${encodeURIComponent(cursor)}`);
  const path = `/v1/children/${childId}/chapters/${encodeURIComponent(chapterId)}/attempts`;
  const page = await api.get(`${path}?${params.join('&')}`, ChapterHistory);
  return { items: page.items, nextCursor: page.nextCursor, meta: page.chapter };
}
