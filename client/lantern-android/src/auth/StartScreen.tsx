import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { Mascot } from '../shared/ui/Mascot';
import { colors, fonts } from '../shared/ui/theme';
import { FeatureList } from './FeatureList';
import { GoogleButton } from './GoogleButton';
import { useGoogleSignIn } from './useGoogleSignIn';

export function StartScreen() {
  const { status, signIn } = useGoogleSignIn();

  return (
    <SafeAreaView style={styles.screen}>
      <ScrollView contentContainerStyle={styles.content}>
        <Text style={styles.wordmark}>Lantern</Text>
        <View style={styles.mascot}>
          <Mascot />
        </View>
        <Text accessibilityRole="header" style={styles.heading}>
          Help your child learn from their own school books.
        </Text>
        <Text style={styles.subheading}>
          Lantern is a non-commercial tool that helps parents support their child&apos;s school
          learning.
        </Text>
        <FeatureList />
      </ScrollView>
      <View style={styles.footer}>
        <Text accessibilityLiveRegion="polite" style={styles.error}>
          {status === 'failed' ? "Couldn't sign in. Try again." : ''}
        </Text>
        <GoogleButton busy={status === 'signingIn'} onPress={signIn} />
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: colors.ground },
  content: { padding: 24, paddingBottom: 12, gap: 12 },
  wordmark: {
    textAlign: 'center',
    fontFamily: fonts.bold,
    fontSize: 28,
    lineHeight: 36,
    color: colors.primary,
  },
  mascot: { alignItems: 'center' },
  heading: {
    textAlign: 'center',
    fontFamily: fonts.bold,
    fontSize: 28,
    lineHeight: 34,
    color: colors.text,
  },
  subheading: {
    textAlign: 'center',
    fontFamily: fonts.medium,
    fontSize: 16,
    lineHeight: 22,
    color: colors.textSecondary,
  },
  footer: { paddingHorizontal: 24, paddingBottom: 24, gap: 8 },
  error: {
    textAlign: 'center',
    fontFamily: fonts.semiBold,
    fontSize: 15,
    color: colors.error,
  },
});
