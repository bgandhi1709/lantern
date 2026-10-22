import { useState } from 'react';

import { MAX_CHILDREN } from '../registration';
import { ChaptersView } from './ChaptersView';
import { useChapters } from './useChapters';

type Props = {
  childId: string;
  subjectId: string;
  onBack: () => void;
  onOpenTab: () => void;
  onAddChild: () => void;
};

export function ChaptersScreen({ childId, subjectId, onBack, onOpenTab, onAddChild }: Props) {
  const chapters = useChapters(childId, subjectId);
  const [sheetOpen, setSheetOpen] = useState(false);
  const [subjectSheetOpen, setSubjectSheetOpen] = useState(false);

  return (
    <ChaptersView
      activeChild={chapters.activeChild}
      switcherChildren={chapters.children}
      subjects={chapters.subjects}
      subjectId={chapters.subjectId}
      chapters={chapters.state}
      sheetOpen={sheetOpen}
      subjectSheetOpen={subjectSheetOpen}
      canAddChild={chapters.children.length < MAX_CHILDREN}
      onBack={onBack}
      onOpenSheet={() => setSheetOpen(true)}
      onOpenSubjectSheet={() => setSubjectSheetOpen(true)}
      onCloseSubjectSheet={() => setSubjectSheetOpen(false)}
      onCloseSheet={() => setSheetOpen(false)}
      onPickChild={(id) => {
        chapters.switchChild(id);
        setSheetOpen(false);
      }}
      onAddChild={() => {
        setSheetOpen(false);
        onAddChild();
      }}
      onSelectSubject={(id) => {
        chapters.selectSubject(id);
        setSubjectSheetOpen(false);
      }}
      onOpenTab={onOpenTab}
      onTryAgain={chapters.retry}
    />
  );
}
