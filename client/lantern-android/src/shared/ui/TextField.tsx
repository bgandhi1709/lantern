import type { ReactNode } from 'react';
import { Text, TextInput, View } from 'react-native';

import { colors } from './theme';

type Props = {
  label: string;
  value: string;
  onChangeText: (text: string) => void;
  maxLength: number;
  hint?: string;
  placeholder?: string;
  icon?: ReactNode;
  // A secret: hidden as it is typed, never capitalised, corrected or offered to the keyboard's memory.
  secret?: boolean;
  error?: string;
  // For the Maestro flows, which find a field with no placeholder by its id.
  testID?: string;
};

// The label is the field's accessibility label, so a screen reader says it once, on the field.
export function TextField({
  label,
  value,
  onChangeText,
  maxLength,
  hint,
  placeholder,
  icon,
  secret = false,
  error,
  testID,
}: Props) {
  return (
    <View>
      <Text importantForAccessibility="no" className="font-inter-bold text-body text-ink">
        {label}
      </Text>
      <View className="mt-1.5 justify-center">
        {icon ? <View className="absolute left-3.5 z-10">{icon}</View> : null}
        <TextInput
          testID={testID}
          accessibilityLabel={label}
          value={value}
          onChangeText={onChangeText}
          placeholder={placeholder}
          placeholderTextColor={colors.textSecondary}
          maxLength={maxLength}
          secureTextEntry={secret}
          autoCapitalize={secret ? 'none' : 'words'}
          autoCorrect={!secret}
          autoComplete="off"
          importantForAutofill={secret ? 'no' : 'auto'}
          returnKeyType="done"
          className={`h-14 rounded-control border-2 border-field bg-card pr-4 font-inter-medium text-lead text-ink focus:border-primary-deep ${icon ? 'pl-12' : 'pl-4'}`}
        />
      </View>
      {hint ? <Text className="mt-1 font-inter-medium text-small text-muted">{hint}</Text> : null}
      {error ? (
        <Text
          accessibilityLiveRegion="polite"
          className="mt-1 font-inter-semibold text-small text-danger"
        >
          {error}
        </Text>
      ) : null}
    </View>
  );
}
