import type { ReactNode } from 'react';
import { Pressable, Text, View } from 'react-native';

type ChoiceRole = 'button' | 'radio';

type Props = {
  role: ChoiceRole;
  selected: boolean;
  label: string;
  onPress: () => void;
  leading: ReactNode;
  title: string;
  subtitle: string;
  trailing: ReactNode;
};

export function ChoiceRow({
  role,
  selected,
  label,
  onPress,
  leading,
  title,
  subtitle,
  trailing,
}: Props) {
  return (
    <Pressable
      accessibilityRole={role}
      accessibilityState={role === 'radio' ? { checked: selected } : { selected }}
      accessibilityLabel={label}
      onPress={onPress}
      className={`lantern-pick-row ${selected ? 'lantern-pick-row-selected' : 'lantern-pick-row-unselected'}`}
    >
      {leading}
      <View className="flex-1">
        <Text className="lantern-item-title">{title}</Text>
        <Text className="lantern-hint">{subtitle}</Text>
      </View>
      {selected ? trailing : null}
    </Pressable>
  );
}
