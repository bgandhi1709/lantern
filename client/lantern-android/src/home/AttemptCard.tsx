import { Text, View } from 'react-native';

import { shortDate } from '../shared/format/shortDate';
import { useStrings } from '../shared/i18n';
import type { Attempt } from './attemptsApi';
import { CheckRow } from './CheckRow';
import type { ChapterState } from './chaptersApi';

const CHIP: Record<ChapterState, { box: string; label: string }> = {
  'not-started': { box: 'bg-track', label: 'text-ink' },
  planned: { box: 'bg-tile', label: 'text-primary-deep' },
  taught: { box: 'bg-warning/15', label: 'text-warning' },
  checked: { box: 'bg-success/15', label: 'text-success' },
};

export function AttemptCard({ attempt }: { attempt: Attempt }) {
  const strings = useStrings().home;
  const text = strings.history;
  const chip = CHIP[attempt.state];

  return (
    <View className="gap-2.5 rounded-card border border-line bg-card px-4 py-3.5">
      <View className="flex-row items-center gap-2">
        <View className="flex-1">
          <Text className="font-inter-bold text-body text-ink">
            {text.attemptTitle(attempt.attemptNumber)}
          </Text>
          <Text className="font-inter-medium text-caption text-muted">
            {text.plannedOn(shortDate(attempt.plannedAt))}
          </Text>
        </View>
        <View className={`rounded-full px-3 py-1 ${chip.box}`}>
          <Text className={`font-inter-bold text-caption ${chip.label}`}>
            {strings.chapters.states[attempt.state]}
          </Text>
        </View>
      </View>
      <Text className="font-inter-medium text-small text-muted">
        {strings.chapters.conceptsDone(attempt.conceptsDone, attempt.conceptCount)}
      </Text>
      {attempt.checks.length === 0 ? (
        <Text className="font-inter-medium text-small text-muted">{text.noCheck}</Text>
      ) : (
        attempt.checks.map((check) => <CheckRow key={check.checkId} check={check} />)
      )}
    </View>
  );
}
