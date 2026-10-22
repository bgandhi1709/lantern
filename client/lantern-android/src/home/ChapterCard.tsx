import { Text, View } from 'react-native';

import type { Chapter } from './chaptersApi';

export function ChapterCard({ chapter }: { chapter: Chapter }) {
  return (
    <View className="flex-row items-start gap-3 rounded-card border border-line bg-card px-4 py-3.5">
      <View
        importantForAccessibility="no-hide-descendants"
        className="size-8 items-center justify-center rounded-full bg-tile"
      >
        <Text className="font-inter-bold text-small text-primary-deep">{chapter.number}</Text>
      </View>
      <View className="flex-1">
        <Text className="font-inter-bold text-body text-ink">{chapter.title}</Text>
        <Text className="mt-0.5 font-inter-medium text-small text-muted">{chapter.summary}</Text>
      </View>
    </View>
  );
}
