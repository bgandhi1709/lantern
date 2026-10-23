import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import type { ChapterState, SubjectProgress } from './chaptersApi';

const SEGMENT: Record<ChapterState, string> = {
  checked: 'bg-primary',
  taught: 'bg-warning',
  planned: 'border border-primary bg-tile',
  'not-started': 'bg-track',
};

type Props = { subjectName: string; childName: string; progress: SubjectProgress };

export function ChaptersSummaryCard({ subjectName, childName, progress }: Props) {
  const text = useStrings().home.chapters;
  const legend: [ChapterState, string, number][] = [
    ['checked', text.legend.checked(progress.checked), progress.checked],
    ['taught', text.legend.taught(progress.taught), progress.taught],
    ['planned', text.legend.planned(progress.planned), progress.planned],
    ['not-started', text.legend.notStarted(progress.notStarted), progress.notStarted],
  ];

  return (
    <View className="gap-2.5 rounded-card border border-line bg-card px-4 py-3.5">
      <View className="flex-row items-baseline justify-between gap-2">
        <Text accessibilityRole="header" className="shrink font-inter-bold text-lead text-ink">
          {text.summaryTitle(subjectName, childName)}
        </Text>
        <Text className="font-inter-bold text-small text-primary-deep">
          {text.checkedOf(progress.checked, progress.states.length)}
        </Text>
      </View>
      <View importantForAccessibility="no-hide-descendants" className="flex-row gap-0.5">
        {progress.states.map((state, index) => (
          <View key={index} className={`h-2 flex-1 rounded-full ${SEGMENT[state]}`} />
        ))}
      </View>
      <View className="flex-row flex-wrap gap-x-3.5 gap-y-1">
        {legend
          .filter(([, , count]) => count > 0)
          .map(([state, label]) => (
            <View key={state} className="flex-row items-center gap-1.5">
              <View
                importantForAccessibility="no-hide-descendants"
                className={`size-2.5 rounded-full ${SEGMENT[state]}`}
              />
              <Text className="font-inter-medium text-caption text-muted">{label}</Text>
            </View>
          ))}
      </View>
    </View>
  );
}
