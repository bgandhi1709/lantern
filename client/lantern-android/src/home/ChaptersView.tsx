import { ActivityIndicator, ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { useColors } from '../shared/theme';
import { BackButton } from '../shared/ui/BackButton';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { ChapterCard } from './ChapterCard';
import { ChildPill } from './ChildPill';
import { ChildSheet } from './ChildSheet';
import type { Subject } from './homeApi';
import { HomeTabs } from './HomeTabs';
import { SubjectPill } from './SubjectPill';
import { SubjectSheet } from './SubjectSheet';
import type { ChaptersState } from './useChapters';
import type { HomeChild, SwitcherChild } from './useHome';

type Props = {
  activeChild: HomeChild | null;
  switcherChildren: SwitcherChild[];
  subjects: Subject[];
  subjectId: string | null;
  chapters: ChaptersState;
  sheetOpen: boolean;
  subjectSheetOpen: boolean;
  canAddChild: boolean;
  onBack: () => void;
  onOpenSheet: () => void;
  onOpenSubjectSheet: () => void;
  onCloseSubjectSheet: () => void;
  onCloseSheet: () => void;
  onPickChild: (childId: string) => void;
  onAddChild: () => void;
  onSelectSubject: (subjectId: string) => void;
  onOpenTab: () => void;
  onTryAgain: () => void;
};

export function ChaptersView(props: Props) {
  const { activeChild, chapters } = props;
  const home = useStrings().home;
  const text = home.chapters;
  const colors = useColors();
  const subjectName = props.subjects.find((subject) => subject.subjectId === props.subjectId)?.name;

  return (
    <View className="flex-1 bg-ground pt-safe">
      <View className="h-16 flex-row items-center gap-1 pl-4 pr-3">
        <BackButton onBack={props.onBack} />
        <Text accessibilityRole="header" className="flex-1 font-inter-bold text-lead text-ink">
          {text.title}
        </Text>
        {activeChild ? <ChildPill name={activeChild.name} onPress={props.onOpenSheet} /> : null}
      </View>
      <ScrollView contentContainerClassName="gap-4 px-4 pb-4 pt-1">
        {subjectName ? (
          <View className="flex-row items-center gap-2">
            <Text className="font-inter-semibold text-small text-muted">{text.subjectLabel}</Text>
            <SubjectPill name={subjectName} onPress={props.onOpenSubjectSheet} />
          </View>
        ) : null}
        {activeChild && subjectName ? (
          <Text className="font-inter-medium text-small text-muted">
            {text.caption(subjectName, activeChild.name)}
          </Text>
        ) : null}
        {chapters.status === 'ready' ? (
          <View className="gap-2">
            {chapters.chapters.map((chapter) => (
              <ChapterCard key={chapter.chapterId} chapter={chapter} />
            ))}
          </View>
        ) : null}
        {chapters.status === 'loading' ? (
          <View
            accessible
            accessibilityLiveRegion="polite"
            accessibilityLabel={text.loading}
            className="flex-row items-center gap-3 py-4"
          >
            <ActivityIndicator color={colors.accent} />
            <Text className="shrink font-inter-medium text-small text-muted">{text.loading}</Text>
          </View>
        ) : null}
        {chapters.status === 'failed' ? (
          <View className="items-start gap-3 py-2">
            <Text
              accessibilityLiveRegion="polite"
              className="font-inter-medium text-body text-danger"
            >
              {text.failed}
            </Text>
            <PrimaryButton label={home.tryAgain} onPress={props.onTryAgain} />
          </View>
        ) : null}
      </ScrollView>
      <HomeTabs onOpenTab={props.onOpenTab} />
      <SubjectSheet
        visible={props.subjectSheetOpen}
        subjects={props.subjects}
        selectedId={props.subjectId}
        onPick={props.onSelectSubject}
        onClose={props.onCloseSubjectSheet}
      />
      {activeChild ? (
        <ChildSheet
          visible={props.sheetOpen}
          activeId={activeChild.id}
          onPick={props.onPickChild}
          onClose={props.onCloseSheet}
          onAddChild={props.canAddChild ? props.onAddChild : undefined}
          switcherChildren={props.switcherChildren}
        />
      ) : null}
    </View>
  );
}
