import type { ReactNode } from 'react';
import { Pressable, Text, View } from 'react-native';

export type OutlineTone = 'primary' | 'danger';

type Props = {
  label: string;
  onPress: () => void;
  icon?: ReactNode;
  // A label read aloud instead of the visible one, when the visible one needs more to be clear.
  accessibilityLabel?: string;
  // Danger is for an action that throws away what the Parent entered.
  tone?: OutlineTone;
};

const TONE_CLASSES: Record<OutlineTone, { border: string; text: string }> = {
  primary: { border: 'border-primary', text: 'text-primary' },
  danger: { border: 'border-danger', text: 'text-danger' },
};

// The quieter button beside a PrimaryButton: the same size, an outline instead of a fill.
export function OutlineButton({
  label,
  onPress,
  icon,
  accessibilityLabel,
  tone = 'primary',
}: Props) {
  const classes = TONE_CLASSES[tone];
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel}
      onPress={onPress}
      className={`min-h-12 flex-row items-center justify-center gap-2 rounded-control border-2 bg-card px-6 py-3 ${classes.border}`}
    >
      {icon ? <View>{icon}</View> : null}
      <Text className={`shrink text-center font-inter-bold text-body ${classes.text}`}>
        {label}
      </Text>
    </Pressable>
  );
}
