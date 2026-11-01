import { Text, View } from 'react-native';

import { BackButton } from '../shared/ui/BackButton';
import { useStrings } from '../shared/i18n';

export function ComingNextView({ onBack }: { onBack: () => void }) {
  const comingNext = useStrings().home.comingNext;

  return (
    <View className="screen gap-3">
      <View className="flex-row">
        <BackButton onBack={onBack} />
      </View>
      <Text accessibilityRole="header" className="type-heading">
        {comingNext.heading}
      </Text>
      <Text className="type-body-muted">{comingNext.text}</Text>
    </View>
  );
}
