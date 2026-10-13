import { Picker as NativePicker } from '@react-native-picker/picker';
import type { ReactNode } from 'react';
import { Text, View } from 'react-native';

import { colors } from './theme';

export type PickerOption = { value: string; label: string };

type Props = {
  label: string;
  value: string;
  options: PickerOption[];
  // The first line, which stands for "nothing chosen yet" and has an empty value.
  placeholder: string;
  onChange: (value: string) => void;
  icon?: ReactNode;
  hint?: string;
  testID?: string;
};

// The phone's own dropdown, so the list looks and reads aloud like every other Android list.
export function Picker({
  label,
  value,
  options,
  placeholder,
  onChange,
  icon,
  hint,
  testID,
}: Props) {
  return (
    <View>
      <Text importantForAccessibility="no" className="font-inter-bold text-body text-ink">
        {label}
      </Text>
      <View className="mt-1.5 h-14 justify-center rounded-control border-2 border-field bg-card">
        {icon ? <View className="absolute left-3.5 z-10">{icon}</View> : null}
        <View className={icon ? 'pl-9' : ''}>
          <NativePicker
            testID={testID}
            accessibilityLabel={label}
            selectedValue={value}
            onValueChange={onChange}
            mode="dropdown"
            dropdownIconColor={colors.text}
          >
            <NativePicker.Item label={placeholder} value="" color={colors.textSecondary} />
            {options.map((option) => (
              <NativePicker.Item key={option.value} label={option.label} value={option.value} />
            ))}
          </NativePicker>
        </View>
      </View>
      {hint ? <Text className="mt-1 font-inter-medium text-small text-muted">{hint}</Text> : null}
    </View>
  );
}
