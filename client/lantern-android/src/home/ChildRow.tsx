import { Pressable, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { ChildAvatar } from './ChildAvatar';
import { TickIcon } from './homeIcons';
import type { SwitcherChild } from './useHome';

type Props = { child: SwitcherChild; selected: boolean; onPick: () => void };

export function ChildRow({ child, selected, onPick }: Props) {
  const home = useStrings().home;
  const status = child.stillBeingMade
    ? home.gettingReady(child.classLevel)
    : home.classLabel(child.classLevel);

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ selected }}
      accessibilityLabel={`${child.name}, ${status}`}
      onPress={onPick}
      className={`min-h-16 flex-row items-center gap-3 rounded-card border-2 px-3.5 py-2.5 ${selected ? 'border-primary bg-tile' : 'border-field bg-card'}`}
    >
      <ChildAvatar name={child.name} large />
      <View className="flex-1">
        <Text className="font-inter-bold text-lead text-ink">{child.name}</Text>
        <Text className="font-inter-medium text-small text-muted">{status}</Text>
      </View>
      {selected ? (
        <View className="size-7 items-center justify-center rounded-full bg-primary">
          <TickIcon />
        </View>
      ) : null}
    </Pressable>
  );
}
