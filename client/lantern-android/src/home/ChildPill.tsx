import { Pressable, Text } from 'react-native';

import { useStrings } from '../shared/i18n';
import { ChildAvatar } from './ChildAvatar';
import { ChevronDownIcon } from './homeIcons';

type Props = { name: string; onPress: () => void };

export function ChildPill({ name, onPress }: Props) {
  const home = useStrings().home;

  return (
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
  );
}
