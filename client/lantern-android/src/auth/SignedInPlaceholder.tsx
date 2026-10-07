import { StyleSheet, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { colors, fonts } from '../shared/ui/theme';
import { signOutOfLantern } from './googleSignIn';
import { type MeStatus, useMeStatus } from './useMeStatus';

// Stands in for Home until the registration and Home stories replace it.
export function SignedInPlaceholder() {
  const { status, retry } = useMeStatus();
  const strings = useStrings();
  const message: Record<MeStatus, string> = strings.signedIn;

  return (
    <View style={styles.screen}>
      <Text accessibilityRole="header" style={styles.wordmark}>
        {strings.appName}
      </Text>
      <Text accessibilityLiveRegion="polite" style={styles.status}>
        {message[status]}
      </Text>
      <PrimaryButton label={strings.signedIn.checkAgain} onPress={retry} />
      <PrimaryButton label={strings.signedIn.signOut} onPress={signOutOfLantern} />
    </View>
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
});
