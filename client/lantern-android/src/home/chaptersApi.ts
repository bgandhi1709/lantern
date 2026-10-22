import { z } from 'zod';

import { api } from '../shared/api';

export const CHAPTERS_PAGE_SIZE = 20;

export const CHAPTER_STATES = ['not-started', 'planned', 'taught', 'checked'] as const;
export type ChapterState = (typeof CHAPTER_STATES)[number];

const ChaptersPage = z.object({
  items: z.array(
    z.object({
      chapterId: z.string(),
      number: z.number().int(),
      title: z.string(),
      summary: z.string(),
      state: z.enum(CHAPTER_STATES),
      attemptCount: z.number().int(),
      conceptsDone: z.number().int().nullable(),
      conceptCount: z.number().int().nullable(),
    }),
  ),
  nextCursor: z.string().nullable(),
  progress: z.object({
    notStarted: z.number().int(),
    planned: z.number().int(),
    taught: z.number().int(),
    checked: z.number().int(),
    states: z.array(z.enum(CHAPTER_STATES)),
  }),
});

export type Chapter = z.infer<typeof ChaptersPage>['items'][number];
export type SubjectProgress = z.infer<typeof ChaptersPage>['progress'];

export async function fetchChaptersPage(
  childId: string,
  subjectId: string,
  query: string,
  cursor: string | null,
) {
  const params = [`limit=${CHAPTERS_PAGE_SIZE}`];
  if (query !== '') params.push(`q=${encodeURIComponent(query)}`);
  if (cursor !== null) params.push(`cursor=${encodeURIComponent(cursor)}`);
  const path = `/v1/children/${childId}/subjects/${encodeURIComponent(subjectId)}/chapters`;
  const page = await api.get(`${path}?${params.join('&')}`, ChaptersPage);
  return { items: page.items, nextCursor: page.nextCursor, meta: page.progress };
}
