import { Pressable, StyleSheet, Text, View } from 'react-native';

import { GoogleG } from '../shared/ui/icons';
import { colors, fonts } from '../shared/ui/theme';

type Props = { busy: boolean; onPress: () => void };

export function GoogleButton({ busy, onPress }: Props) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ disabled: busy, busy }}
      disabled={busy}
      onPress={onPress}
      style={({ pressed }) => [styles.button, (pressed || busy) && styles.dimmed]}
    >
      <View style={styles.logoTile}>
        <GoogleG />
      </View>
      <Text style={styles.label}>{busy ? 'Signing in…' : 'Continue with Google'}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  button: {
    height: 56,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 12,
    borderRadius: 12,
    backgroundColor: colors.primary,
  },
  dimmed: { opacity: 0.7 },
  logoTile: {
    width: 30,
    height: 30,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 8,
    backgroundColor: colors.card,
  },
  label: { fontFamily: fonts.bold, fontSize: 16, color: colors.onPrimary },
});
