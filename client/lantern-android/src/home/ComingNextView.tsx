import { Text, View } from 'react-native';

import { BackButton } from '../shared/ui/BackButton';
import { useStrings } from '../shared/i18n';

export function ComingNextView({ onBack }: { onBack: () => void }) {
  const comingNext = useStrings().home.comingNext;

  return (
    <View className="flex-1 gap-3 bg-ground px-6 pb-safe pt-safe">
      <View className="flex-row">
        <BackButton onBack={onBack} />
      </View>
      <Text accessibilityRole="header" className="font-inter-bold text-heading text-ink">
        {comingNext.heading}
      </Text>
      <Text className="font-inter-medium text-body text-muted">{comingNext.text}</Text>
    </View>
  );
}
