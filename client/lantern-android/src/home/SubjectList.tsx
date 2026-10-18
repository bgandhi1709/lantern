import { ActivityIndicator, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { colors } from '../shared/ui/theme';
import { SubjectRow } from './SubjectRow';
import type { SubjectsState } from './useHome';

type Props = {
  childName: string;
  classLevel: number;
  state: SubjectsState;
  onOpenSubject: (subjectId: string) => void;
  onTryAgain: () => void;
};

export function SubjectList({ childName, classLevel, state, onOpenSubject, onTryAgain }: Props) {
  const home = useStrings().home;
  const loadingLabel = home.subjectsLoading(childName);

  return (
    <View className="gap-2">
      <Text accessibilityRole="header" className="font-inter-bold text-body text-ink">
        {home.subjectsTitle(childName, classLevel)}
      </Text>
      {state.status === 'ready'
        ? state.items.map((subject) => (
            <SubjectRow
              key={subject.subjectId}
              subject={subject}
              onPress={() => onOpenSubject(subject.subjectId)}
            />
          ))
        : null}
      {state.status === 'loading' ? (
        <View
          accessible
          accessibilityLiveRegion="polite"
          accessibilityLabel={loadingLabel}
          className="flex-row items-center gap-3 py-4"
        >
          <ActivityIndicator color={colors.primary} />
          <Text className="shrink font-inter-medium text-small text-muted">{loadingLabel}</Text>
        </View>
      ) : null}
      {state.status === 'failed' ? (
        <View className="items-start gap-3 py-2">
          <Text
            accessibilityLiveRegion="polite"
            className="font-inter-medium text-body text-danger"
          >
            {home.subjectsFailed}
          </Text>
          <PrimaryButton label={home.tryAgain} onPress={onTryAgain} />
        </View>
      ) : null}
    </View>
  );
}
