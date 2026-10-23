import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { shortDate } from '../shared/format/shortDate';
import type { Check } from './attemptsApi';

const STRONG_MARK = 0.8;

export function CheckRow({ check }: { check: Check }) {
  const text = useStrings().home.history;
  const strong = check.right / check.of >= STRONG_MARK;

  return (
    <View className="flex-row items-start gap-3 border-t border-line pt-2.5">
      <View className="flex-1">
        <Text className="font-inter-semibold text-body text-ink">
          {text.checkTitle(check.checkNumber)}
        </Text>
        <Text className="font-inter-medium text-caption text-muted">
          {shortDate(check.takenAt)}
        </Text>
        <Text className="mt-0.5 font-inter-medium text-small text-muted">
          {text.wrong(check.wrong.map((concept) => concept.title))}
        </Text>
      </View>
      <Text className={`font-inter-bold text-lead ${strong ? 'text-success' : 'text-warning'}`}>
        {text.mark(check.right, check.of)}
      </Text>
    </View>
  );
}
