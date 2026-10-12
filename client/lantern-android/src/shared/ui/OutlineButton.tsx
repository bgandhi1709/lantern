import type { ReactNode } from 'react';
import { Pressable, Text, View } from 'react-native';

type Props = { label: string; onPress: () => void; icon?: ReactNode };

// The quieter button beside a PrimaryButton: the same size, an outline instead of a fill.
export function OutlineButton({ label, onPress, icon }: Props) {
  return (
    <Pressable
      accessibilityRole="button"
      onPress={onPress}
      className="min-h-12 flex-row items-center justify-center gap-2 rounded-control border-2 border-primary bg-card px-6 py-3"
    >
      {icon ? <View>{icon}</View> : null}
      <Text className="shrink text-center font-inter-bold text-body text-primary">{label}</Text>
    </Pressable>
  );
}
