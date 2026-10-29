import { ChapterHistoryView } from './ChapterHistoryView';
import { useChapterHistory } from './useChapterHistory';

type Props = { childId: string; chapterId: string; onBack: () => void; onAction: () => void };

export function ChapterHistoryScreen({ childId, chapterId, onBack, onAction }: Props) {
  const history = useChapterHistory(childId, chapterId);

  return (
    <ChapterHistoryView
      history={history}
      onBack={onBack}
      onAction={onAction}
      onLoadMore={history.loadMore}
      onRetryMore={history.retryMore}
      onTryAgain={history.reload}
    />
  );
}
