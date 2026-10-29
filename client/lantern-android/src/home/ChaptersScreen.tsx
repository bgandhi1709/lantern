import { useState } from 'react';

import { MAX_CHILDREN } from '../registration';
import { tapped, useTrackScreen } from '../shared/analytics';
import type { Board } from '../registration';
import { ChaptersView } from './ChaptersView';
import { useChapters } from './useChapters';

type Props = {
  childId: string;
  subjectId: string;
  onBack: () => void;
  onOpenTab: () => void;
  onAddChild: (board: Board) => void;
  onAction: () => void;
  onOpenHistory: (childId: string, chapterId: string) => void;
};

export function ChaptersScreen({
  childId,
  subjectId,
  onBack,
  onOpenTab,
  onAddChild,
  onAction,
  onOpenHistory,
}: Props) {
  useTrackScreen('chapters');
  const chapters = useChapters(childId, subjectId);
  const [sheetOpen, setSheetOpen] = useState(false);
  const [subjectSheetOpen, setSubjectSheetOpen] = useState(false);

  return (
    <ChaptersView
      activeChild={chapters.activeChild}
      switcherChildren={chapters.children}
      subjects={chapters.subjects}
      subjectId={chapters.subjectId}
      chapters={chapters.chapters}
      query={chapters.query}
      sheetOpen={sheetOpen}
      subjectSheetOpen={subjectSheetOpen}
      canAddChild={chapters.board !== null && chapters.children.length < MAX_CHILDREN}
      onBack={onBack}
      onOpenSheet={() => setSheetOpen(true)}
      onOpenSubjectSheet={() => setSubjectSheetOpen(true)}
      onCloseSubjectSheet={() => setSubjectSheetOpen(false)}
      onCloseSheet={() => setSheetOpen(false)}
      onPickChild={tapped('switch-child', (id: string) => {
        chapters.switchChild(id);
        setSheetOpen(false);
      })}
      onAddChild={tapped('add-child', () => {
        setSheetOpen(false);
        if (chapters.board) onAddChild(chapters.board);
      })}
      onSelectSubject={(id) => {
        chapters.selectSubject(id);
        setSubjectSheetOpen(false);
      }}
      onChangeQuery={chapters.setQuery}
      onAction={onAction}
      onOpenHistory={tapped('open-chapter-history', (chapterId: string) =>
        onOpenHistory(chapters.childId, chapterId),
      )}
      onLoadMore={chapters.loadMore}
      onRetryMore={chapters.retryMore}
      onOpenTab={onOpenTab}
      onTryAgain={chapters.retry}
    />
  );
}
