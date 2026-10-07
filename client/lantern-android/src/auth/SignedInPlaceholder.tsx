import { Pressable, StyleSheet, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { colors, fonts, MIN_TOUCH_TARGET } from '../shared/ui/theme';
import { signOutOfLantern } from './googleSignIn';
import { type MeStatus, useMeStatus } from './useMeStatus';

// Stands in for Home until the registration and Home stories replace it.
export function SignedInPlaceholder() {
  const { status, retry } = useMeStatus();
  const s = useStrings();
  const message: Record<MeStatus, string> = s.signedIn;

  return (
    <View style={styles.screen}>
      <Text accessibilityRole="header" style={styles.wordmark}>
        {s.appName}
      </Text>
      <Text accessibilityLiveRegion="polite" style={styles.status}>
        {message[status]}
      </Text>
      <Button label={s.signedIn.checkAgain} onPress={retry} />
      <Button label={s.signedIn.signOut} onPress={signOutOfLantern} />
    </View>
  );
}

function Button({ label, onPress }: { label: string; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" onPress={onPress} style={styles.button}>
      <Text style={styles.buttonText}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 16,
    padding: 24,
    backgroundColor: colors.ground,
  },
  wordmark: { fontFamily: fonts.bold, fontSize: 36, color: colors.primary },
  status: {
    textAlign: 'center',
    fontFamily: fonts.medium,
    fontSize: 16,
    color: colors.textSecondary,
  },
  button: {
    minHeight: MIN_TOUCH_TARGET,
    justifyContent: 'center',
    paddingHorizontal: 24,
    borderRadius: 12,
    backgroundColor: colors.primary,
  },
  buttonText: { fontFamily: fonts.bold, fontSize: 16, color: colors.onPrimary },
});
