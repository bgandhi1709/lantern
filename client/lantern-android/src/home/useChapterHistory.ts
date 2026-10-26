import { usePagedList } from '../shared/paging/usePagedList';
import { fetchAttemptsPage } from './attemptsApi';

export function useChapterHistory(childId: string, chapterId: string) {
  return usePagedList({
    key: `${childId}/${chapterId}`,
    load: (cursor) => fetchAttemptsPage(childId, chapterId, cursor),
  });
}

export type ChapterHistoryList = ReturnType<typeof useChapterHistory>;
