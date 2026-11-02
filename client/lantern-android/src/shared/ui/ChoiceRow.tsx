import type { ReactNode } from 'react';
import { Pressable, Text, View } from 'react-native';

export type ChoiceRole = 'button' | 'radio';

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
      className={`choice-row ${selected ? 'choice-on' : 'choice-off'}`}
    >
      {leading}
      <View className="flex-1">
        <Text className="type-lead">{title}</Text>
        <Text className="type-small-muted">{subtitle}</Text>
      </View>
      {selected ? trailing : null}
    </Pressable>
  );
}
