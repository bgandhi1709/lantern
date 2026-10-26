import { Pressable, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { stateLine } from './chapterLabels';
import { ChapterStateTracker } from './ChapterStateTracker';
import type { Chapter } from './chaptersApi';
import { HistoryIcon } from './homeIcons';

type Props = { chapter: Chapter; onAction: () => void; onOpenHistory: () => void };

export function ChapterCard({ chapter, onAction, onOpenHistory }: Props) {
  const text = useStrings().home.chapters;
  const line = stateLine(text, chapter);
  const action = text.actions[chapter.state];
  const attempts = text.attempts(chapter.attemptCount);
  const outlined = chapter.state === 'checked';

  return (
    <View className="overflow-hidden rounded-card border border-line bg-card">
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={`${chapter.title}, ${line}. ${action}`}
        onPress={onAction}
        className="gap-3 px-4 py-3.5"
      >
        <View className="flex-row items-start gap-3">
          <View
            importantForAccessibility="no-hide-descendants"
            className="size-8 items-center justify-center rounded-full bg-tile"
          >
            <Text className="font-inter-bold text-small text-primary-deep">{chapter.number}</Text>
          </View>
          <View className="flex-1">
            <Text className="font-inter-bold text-body text-ink">{chapter.title}</Text>
            <Text numberOfLines={2} className="mt-0.5 font-inter-medium text-small text-muted">
              {chapter.summary}
            </Text>
            <Text className="mt-1.5 font-inter-bold text-caption text-primary-deep">{line}</Text>
          </View>
        </View>
        <View className="flex-row items-center gap-3">
          <ChapterStateTracker state={chapter.state} />
          <View
            className={`min-h-12 min-w-26 items-center justify-center rounded-control border-2 border-primary px-4 ${outlined ? 'bg-card' : 'bg-primary'}`}
          >
            <Text
              className={`font-inter-semibold text-small ${outlined ? 'text-primary-deep' : 'text-on-primary'}`}
            >
              {action}
            </Text>
          </View>
        </View>
      </Pressable>
      {chapter.attemptCount > 0 ? (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={text.viewHistoryOf(chapter.title, attempts)}
          onPress={onOpenHistory}
          className="min-h-12 flex-row items-center gap-2 border-t border-line bg-tile px-4"
        >
          <HistoryIcon />
          <Text className="flex-1 font-inter-bold text-small text-primary-deep">{attempts}</Text>
          <Text className="font-inter-bold text-small text-primary-deep underline">
            {text.viewHistory}
          </Text>
        </Pressable>
      ) : null}
    </View>
  );
}
