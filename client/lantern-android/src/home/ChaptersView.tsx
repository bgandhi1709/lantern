import { ActivityIndicator, FlatList, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { useColors } from '../shared/theme';
import { BackButton } from '../shared/ui/BackButton';
import { PagingFooter } from '../shared/ui/PagingFooter';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { ChapterCard } from './ChapterCard';
import { ChapterSearch } from './ChapterSearch';
import { ChaptersSummaryCard } from './ChaptersSummaryCard';
import { ChildPill } from './ChildPill';
import { ChildSheet } from './ChildSheet';
import type { Subject } from './homeApi';
import { HomeTabs } from './HomeTabs';
import { SubjectPill } from './SubjectPill';
import { SubjectSheet } from './SubjectSheet';
import type { ChaptersList } from './useChapters';
import type { HomeChild, SwitcherChild } from './useHome';

const LOAD_MORE_AT_REMAINING = 0.5;

type Props = {
  activeChild: HomeChild | null;
  switcherChildren: SwitcherChild[];
  subjects: Subject[];
  subjectId: string | null;
  chapters: ChaptersList;
  query: string;
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
  onChangeQuery: (query: string) => void;
  onAction: () => void;
  onOpenHistory: (chapterId: string) => void;
  onLoadMore: () => void;
  onRetryMore: () => void;
  onOpenTab: () => void;
  onTryAgain: () => void;
};

export function ChaptersView(props: Props) {
  const { activeChild, chapters } = props;
  const home = useStrings().home;
  const text = home.chapters;
  const colors = useColors();
  const subjectName = props.subjects.find((subject) => subject.subjectId === props.subjectId)?.name;

  const header = (
    <View className="gap-4 pb-4">
      {subjectName ? (
        <View className="flex-row items-center gap-2">
          <Text className="font-inter-semibold text-small text-muted">{text.subjectLabel}</Text>
          <SubjectPill name={subjectName} onPress={props.onOpenSubjectSheet} />
        </View>
      ) : null}
      <ChapterSearch value={props.query} onChange={props.onChangeQuery} />
      {chapters.progress && activeChild && subjectName ? (
        <ChaptersSummaryCard
          subjectName={subjectName}
          childName={activeChild.name}
          progress={chapters.progress}
        />
      ) : null}
    </View>
  );

  const empty =
    chapters.status === 'loading' ? (
      <View
        accessible
        accessibilityLiveRegion="polite"
        accessibilityLabel={text.loading}
        className="flex-row items-center gap-3 py-4"
      >
        <ActivityIndicator color={colors.accent} />
        <Text className="shrink font-inter-medium text-small text-muted">{text.loading}</Text>
      </View>
    ) : chapters.status === 'failed' ? (
      <View className="items-start gap-3 py-2">
        <Text accessibilityLiveRegion="polite" className="font-inter-medium text-body text-danger">
          {text.failed}
        </Text>
        <PrimaryButton label={home.tryAgain} onPress={props.onTryAgain} />
      </View>
    ) : props.query.trim() !== '' && subjectName ? (
      <Text className="rounded-card border border-line bg-card p-4 font-inter-medium text-body text-muted">
        {text.noMatch(subjectName)}
      </Text>
    ) : null;

  return (
    <View className="flex-1 bg-ground pt-safe">
      <View className="h-16 flex-row items-center gap-1 pl-4 pr-3">
        <BackButton onBack={props.onBack} />
        <Text accessibilityRole="header" className="flex-1 font-inter-bold text-lead text-ink">
          {text.title}
        </Text>
        {activeChild ? <ChildPill name={activeChild.name} onPress={props.onOpenSheet} /> : null}
      </View>
      <FlatList
        testID="chapters-list"
        data={chapters.items}
        keyExtractor={(chapter) => chapter.chapterId}
        renderItem={({ item }) => (
          <ChapterCard
            chapter={item}
            onAction={props.onAction}
            onOpenHistory={() => props.onOpenHistory(item.chapterId)}
          />
        )}
        ItemSeparatorComponent={() => <View className="h-2" />}
        ListHeaderComponent={header}
        ListEmptyComponent={empty}
        ListFooterComponent={
          <PagingFooter
            loadingMore={chapters.loadingMore}
            failed={chapters.moreFailed}
            loadingLabel={text.loadingMore}
            failedLabel={text.loadMoreFailed}
            onRetry={props.onRetryMore}
          />
        }
        onEndReached={chapters.status === 'ready' ? props.onLoadMore : undefined}
        onEndReachedThreshold={LOAD_MORE_AT_REMAINING}
        keyboardShouldPersistTaps="handled"
        contentContainerClassName="px-4 pb-4 pt-1"
        className={chapters.refreshing ? 'opacity-60' : ''}
      />
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
