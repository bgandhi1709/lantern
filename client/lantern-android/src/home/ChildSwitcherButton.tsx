import { Pressable, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { ChildAvatar } from './ChildAvatar';
import { ChevronDownIcon } from './homeIcons';

type Props = { name: string; onPress: () => void };

export function ChildSwitcherButton({ name, onPress }: Props) {
  const home = useStrings().home;

  return (
    <View className="flex-row items-center gap-2">
      <Text className="font-inter-semibold text-small text-muted">{home.learningWith}</Text>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={home.switchChild(name)}
        onPress={onPress}
        className="min-h-12 shrink flex-row items-center gap-2 rounded-full border border-line bg-card py-1 pl-1.5 pr-2.5"
      >
        <ChildAvatar name={name} />
        <Text numberOfLines={1} className="shrink font-inter-bold text-small text-ink">
          {name}
        </Text>
        <ChevronDownIcon />
      </Pressable>
    </View>
  );
}
