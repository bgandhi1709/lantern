import { z } from 'zod';

import { api } from '../shared/api';

const PAGE_LIMIT = 100;

const ChaptersPage = z.object({
  items: z.array(
    z.object({
      chapterId: z.string(),
      number: z.number().int(),
      title: z.string(),
      summary: z.string(),
    }),
  ),
  nextCursor: z.string().nullable(),
});

export type Chapter = z.infer<typeof ChaptersPage>['items'][number];

const pathOf = (childId: string, subjectId: string, cursor: string | null) => {
  const base = `/v1/children/${childId}/subjects/${encodeURIComponent(subjectId)}/chapters`;
  const query = `?limit=${PAGE_LIMIT}`;
  return cursor === null ? base + query : `${base}${query}&cursor=${encodeURIComponent(cursor)}`;
};

export async function fetchChapters(childId: string, subjectId: string) {
  const chapters: Chapter[] = [];
  let cursor: string | null = null;
  do {
    const page: z.infer<typeof ChaptersPage> = await api.get(
      pathOf(childId, subjectId, cursor),
      ChaptersPage,
    );
    chapters.push(...page.items);
    cursor = page.nextCursor;
  } while (cursor !== null);
  return chapters;
}
