import type { ReactNode } from 'react';
import { Text, TextInput, View } from 'react-native';

import { colors } from './theme';

type Props = {
  label: string;
  hint: string;
  placeholder: string;
  value: string;
  onChangeText: (text: string) => void;
  icon: ReactNode;
  maxLength: number;
};

// The label is the field's accessibility label, so a screen reader says it once, on the field.
export function TextField({
  label,
  hint,
  placeholder,
  value,
  onChangeText,
  icon,
  maxLength,
}: Props) {
  return (
    <View>
      <Text importantForAccessibility="no" className="font-inter-bold text-body text-ink">
        {label}
      </Text>
      <View className="mt-1.5 justify-center">
        <View className="absolute left-3.5 z-10">{icon}</View>
        <TextInput
          accessibilityLabel={label}
          value={value}
          onChangeText={onChangeText}
          placeholder={placeholder}
          placeholderTextColor={colors.textSecondary}
          maxLength={maxLength}
          autoCapitalize="words"
          autoComplete="off"
          returnKeyType="done"
          className="h-14 rounded-control border-2 border-field bg-card pl-12 pr-4 font-inter-medium text-lead text-ink focus:border-primary-deep"
        />
      </View>
      <Text className="mt-1 font-inter-medium text-small text-muted">{hint}</Text>
    </View>
  );
}
