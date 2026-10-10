import { Pressable, Text } from 'react-native';

type Props = { label: string; onPress: () => void; disabled?: boolean };

// Disabled is dashed and grey as well as unresponsive, so it never relies on colour alone.
export function PrimaryButton({ label, onPress, disabled = false }: Props) {
  return (
    <Pressable
      accessibilityRole="button"
      disabled={disabled}
      onPress={onPress}
      className={`min-h-12 justify-center rounded-control px-6 py-3 ${disabled ? 'border-2 border-dashed border-disabled-line bg-disabled' : 'bg-primary'}`}
    >
      <Text
        className={`text-center font-inter-bold text-body ${disabled ? 'text-disabled-text' : 'text-on-primary'}`}
      >
        {label}
      </Text>
    </Pressable>
  );
}
