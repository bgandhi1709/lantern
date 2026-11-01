import { ActivityIndicator, Text, View } from 'react-native';

import { useStrings } from '../i18n';
import { useColors } from '../theme';
import { PrimaryButton } from './PrimaryButton';

type Props = {
  loadingMore: boolean;
  failed: boolean;
  loadingLabel: string;
  failedLabel: string;
  onRetry: () => void;
};

export function PagingFooter({ loadingMore, failed, loadingLabel, failedLabel, onRetry }: Props) {
  const colors = useColors();
  const home = useStrings().home;

  if (failed) {
    return (
      <View className="items-start gap-3 py-2">
        <Text accessibilityLiveRegion="polite" className="type-body-error">
          {failedLabel}
        </Text>
        <PrimaryButton label={home.tryAgain} onPress={onRetry} />
      </View>
    );
  }
  if (!loadingMore) return null;
  return (
    <View
      accessible
      accessibilityLiveRegion="polite"
      accessibilityLabel={loadingLabel}
      className="flex-row items-center gap-3 py-4"
    >
      <ActivityIndicator color={colors.accent} />
      <Text className="shrink type-small-muted">{loadingLabel}</Text>
    </View>
  );
}
