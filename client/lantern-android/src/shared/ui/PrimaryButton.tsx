import { Pressable, Text } from 'react-native';

export function PrimaryButton({ label, onPress }: { label: string; onPress: () => void }) {
  return (
    <Pressable
      accessibilityRole="button"
      onPress={onPress}
      className="min-h-12 justify-center rounded-control bg-primary px-6 py-3"
    >
      <Text className="text-center font-inter-bold text-body text-on-primary">{label}</Text>
    </Pressable>
  );
}
