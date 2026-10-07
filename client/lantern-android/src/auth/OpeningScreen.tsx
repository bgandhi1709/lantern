import { Animated, StyleSheet, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { colors, fonts } from '../shared/ui/theme';
import { IntroMark } from './IntroMark';
import { PageTurnLoader } from './PageTurnLoader';
import type { useIntro } from './useIntro';

const MARK_SIZE = 200;

// Shown on launch: the intro plays while the saved sign-in is restored. If restoring takes longer
// than the intro, the page-turning book shows Lantern is still opening.
export function OpeningScreen({ intro }: { intro: ReturnType<typeof useIntro> }) {
  const s = useStrings();
  const word = intro.values.word;
  const rise = word.interpolate({ inputRange: [0, 1], outputRange: [8, 0] });

  return (
    <View style={styles.screen}>
      <IntroMark values={intro.values} size={MARK_SIZE} />
      <Animated.Text
        style={[styles.wordmark, { opacity: word, transform: [{ translateY: rise }] }]}
      >
        {s.appName}
      </Animated.Text>
      <View
        accessible
        accessibilityLabel={s.opening.status}
        accessibilityLiveRegion="polite"
        style={styles.status}
      >
        {intro.done ? <PageTurnLoader still={intro.still} /> : null}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 16,
    backgroundColor: colors.ground,
  },
  wordmark: { fontFamily: fonts.bold, fontSize: 40, lineHeight: 48, color: colors.primary },
  status: { height: 48, justifyContent: 'center' },
});
