import { Text, View } from 'react-native';

import { BackButton } from '../shared/ui/BackButton';
import { useStrings } from '../shared/i18n';

export function ComingNextView({ onBack }: { onBack: () => void }) {
  const comingNext = useStrings().home.comingNext;

  return (
    <View className="lantern-screen gap-3">
      <View className="flex-row">
        <BackButton onBack={onBack} />
      </View>
      <Text accessibilityRole="header" className="lantern-screen-title">
        {comingNext.heading}
      </Text>
      <Text className="lantern-intro-text">{comingNext.text}</Text>
    </View>
  );
}
