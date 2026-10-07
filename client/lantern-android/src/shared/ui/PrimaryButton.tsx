import { Pressable, StyleSheet, Text } from 'react-native';

import { colors, fonts, MIN_TOUCH_TARGET } from './theme';

export function PrimaryButton({ label, onPress }: { label: string; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" onPress={onPress} style={styles.button}>
      <Text style={styles.label}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  button: {
    minHeight: MIN_TOUCH_TARGET,
    justifyContent: 'center',
    paddingHorizontal: 24,
    borderRadius: 12,
    backgroundColor: colors.primary,
  },
  label: { fontFamily: fonts.bold, fontSize: 16, color: colors.onPrimary },
});
