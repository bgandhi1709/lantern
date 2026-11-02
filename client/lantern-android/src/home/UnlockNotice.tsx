import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { PrimaryButton } from '../shared/ui/PrimaryButton';

export function UnlockNotice({ onUnlock }: { onUnlock: () => void }) {
  const strings = useStrings().home.locked;

  return (
    <View className="gap-3 lantern-card-padded">
      <Text accessibilityRole="header" className="lantern-item-title">
        {strings.heading}
      </Text>
      <Text className="lantern-hint">{strings.text}</Text>
      <PrimaryButton label={strings.unlock} onPress={onUnlock} />
    </View>
  );
}
