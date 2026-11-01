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
      className={`choice-row ${selected ? 'border-primary bg-tile' : 'border-field bg-card'}`}
    >
      <ChildAvatar name={child.name} large />
      <View className="flex-1">
        <Text className="type-lead">{child.name}</Text>
        <Text className="type-small-muted">{status}</Text>
      </View>
      {selected ? (
        <View className="size-7 items-center justify-center rounded-full bg-primary">
          <TickIcon />
        </View>
      ) : null}
    </Pressable>
  );
}
