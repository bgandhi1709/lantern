import { Pressable, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';

export function UnlockRow({ onPress }: { onPress: () => void }) {
  const strings = useStrings().appearance.unlockRow;

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={strings.title}
      onPress={onPress}
      className="min-h-12 gap-0.5 rounded-card border border-line bg-card p-4"
    >
      <Text className="font-inter-bold text-lead text-ink">{strings.title}</Text>
      <View>
        <Text className="font-inter-medium text-small text-muted">{strings.text}</Text>
      </View>
    </Pressable>
  );
}
