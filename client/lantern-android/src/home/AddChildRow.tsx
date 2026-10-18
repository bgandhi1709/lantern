import { Pressable, Text, View } from 'react-native';

import { MAX_CHILDREN } from '../registration';
import { useStrings } from '../shared/i18n';
import { PlusIcon } from './homeIcons';

export function AddChildRow({ onPress }: { onPress: () => void }) {
  const home = useStrings().home;

  return (
    <View className="gap-3">
      <Pressable
        accessibilityRole="button"
        onPress={onPress}
        className="min-h-14 flex-row items-center gap-3 rounded-card border-2 border-dashed border-field px-3.5"
      >
        <View className="size-11 items-center justify-center rounded-full bg-tile">
          <PlusIcon />
        </View>
        <Text className="font-inter-bold text-body text-primary-deep">{home.addChild}</Text>
      </Pressable>
      <Text className="text-center font-inter-medium text-caption text-muted">
        {home.addChildNote(MAX_CHILDREN)}
      </Text>
    </View>
  );
}
