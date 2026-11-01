import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { PrimaryButton } from '../shared/ui/PrimaryButton';

export function UnlockNotice({ onUnlock }: { onUnlock: () => void }) {
  const strings = useStrings().home.locked;

  return (
    <View className="gap-3 rounded-card border border-line bg-card p-4">
      <Text accessibilityRole="header" className="type-lead">
        {strings.heading}
      </Text>
      <Text className="type-small-muted">{strings.text}</Text>
      <PrimaryButton label={strings.unlock} onPress={onUnlock} />
    </View>
  );
}
