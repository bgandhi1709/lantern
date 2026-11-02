import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import type { ChapterHeader } from './attemptsApi';
import { stateLine } from './chapterLabels';

export function ChapterHistoryHeader({ chapter }: { chapter: ChapterHeader }) {
  const strings = useStrings().home;
  const text = strings.history;

  return (
    <View className="gap-1.5 rounded-3xl border border-line bg-tile p-4">
      <Text className="font-inter-semibold text-caption text-primary-deep">
        {text.subjectChapter(chapter.subjectName, chapter.number)}
      </Text>
      <Text accessibilityRole="header" className="lantern-screen-title">
        {chapter.title}
      </Text>
      <Text className="lantern-chapter-meta">{text.now(stateLine(strings.chapters, chapter))}</Text>
      <Text className="lantern-footnote">
        {text.totals(
          strings.chapters.attempts(chapter.attemptCount),
          text.checks(chapter.checkCount),
        )}
      </Text>
    </View>
  );
}
