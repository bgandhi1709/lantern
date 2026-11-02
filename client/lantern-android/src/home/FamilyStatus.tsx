import { ActivityIndicator, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { useColors } from '../shared/theme';
import { PrimaryButton } from '../shared/ui/PrimaryButton';

type Props = { failed: boolean; onTryAgain: () => void };

export function FamilyStatus({ failed, onTryAgain }: Props) {
  const home = useStrings().home;
  const colors = useColors();

  if (failed) {
    return (
      <View className="items-start gap-3 py-2">
        <Text accessibilityLiveRegion="polite" className="lantern-error-message">
          {home.familyFailed}
        </Text>
        <PrimaryButton label={home.tryAgain} onPress={onTryAgain} />
      </View>
    );
  }
  return (
    <View
      accessible
      accessibilityLiveRegion="polite"
      accessibilityLabel={home.familyLoading}
      className="flex-row items-center gap-3 py-4"
    >
      <ActivityIndicator color={colors.accent} />
      <Text className="shrink lantern-hint">{home.familyLoading}</Text>
    </View>
  );
}
