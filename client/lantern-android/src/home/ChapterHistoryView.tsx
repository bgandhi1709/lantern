import { ActivityIndicator, FlatList, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { useColors } from '../shared/theme';
import { BackButton } from '../shared/ui/BackButton';
import { PagingFooter } from '../shared/ui/PagingFooter';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { AttemptCard } from './AttemptCard';
import { ChapterHistoryHeader } from './ChapterHistoryHeader';
import type { ChapterHistoryList } from './useChapterHistory';

const LOAD_MORE_AT_REMAINING = 0.5;

type Props = {
  history: Pick<ChapterHistoryList, 'status' | 'items' | 'meta' | 'loadingMore' | 'moreFailed'>;
  onBack: () => void;
  onAction: () => void;
  onLoadMore: () => void;
  onRetryMore: () => void;
  onTryAgain: () => void;
};

export function ChapterHistoryView({ history, ...on }: Props) {
  const strings = useStrings().home;
  const text = strings.history;
  const colors = useColors();
  const chapter = history.meta;

  const empty =
    history.status === 'loading' ? (
      <View
        accessible
        accessibilityLiveRegion="polite"
        accessibilityLabel={text.loading}
        className="flex-row items-center gap-3 py-4"
      >
        <ActivityIndicator color={colors.accent} />
        <Text className="shrink font-inter-medium text-small text-muted">{text.loading}</Text>
      </View>
    ) : history.status === 'failed' ? (
      <View className="items-start gap-3 py-2">
        <Text accessibilityLiveRegion="polite" className="font-inter-medium text-body text-danger">
          {text.failed}
        </Text>
        <PrimaryButton label={strings.tryAgain} onPress={on.onTryAgain} />
      </View>
    ) : (
      <Text className="font-inter-medium text-body text-muted">{text.none}</Text>
    );

  return (
    <View className="flex-1 bg-ground pt-safe">
      <View className="h-16 flex-row items-center gap-1 pl-4 pr-3">
        <BackButton onBack={on.onBack} />
        <Text accessibilityRole="header" className="flex-1 font-inter-bold text-lead text-ink">
          {text.title}
        </Text>
      </View>
      <FlatList
        testID="history-list"
        data={history.items}
        keyExtractor={(attempt) => String(attempt.attemptNumber)}
        renderItem={({ item }) => <AttemptCard attempt={item} />}
        ItemSeparatorComponent={() => <View className="h-2.5" />}
        ListHeaderComponent={
          chapter ? (
            <View className="gap-3.5 pb-3.5">
              <ChapterHistoryHeader chapter={chapter} />
              <Text accessibilityRole="header" className="font-inter-bold text-body text-ink">
                {text.attemptsHeading}
              </Text>
            </View>
          ) : null
        }
        ListEmptyComponent={empty}
        ListFooterComponent={
          <PagingFooter
            loadingMore={history.loadingMore}
            failed={history.moreFailed}
            loadingLabel={text.loadingMore}
            failedLabel={text.loadMoreFailed}
            onRetry={on.onRetryMore}
          />
        }
        onEndReached={history.status === 'ready' ? on.onLoadMore : undefined}
        onEndReachedThreshold={LOAD_MORE_AT_REMAINING}
        contentContainerClassName="px-4 pb-4 pt-1"
      />
      {chapter ? (
        <View className="border-t border-line bg-ground px-4 pb-safe pt-3">
          <PrimaryButton label={text.actions[chapter.state]} onPress={on.onAction} />
        </View>
      ) : null}
    </View>
  );
}
