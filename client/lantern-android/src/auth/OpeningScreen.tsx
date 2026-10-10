import { Animated, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { IntroMark } from './IntroMark';
import { PageTurnLoader } from './PageTurnLoader';
import type { Intro } from './useIntro';

// Shown on launch: the intro plays while the saved sign-in is restored. If restoring takes longer
// than the intro, the page-turning book shows Lantern is still opening.
export function OpeningScreen({ intro }: { intro: Intro }) {
  const strings = useStrings();
  const word = intro.values.word;
  const rise = word.interpolate({ inputRange: [0, 1], outputRange: [8, 0] });

  return (
    <View className="flex-1 items-center justify-center gap-4 bg-ground">
      <IntroMark values={intro.values} />
      <Animated.View style={{ opacity: word, transform: [{ translateY: rise }] }}>
        <Text className="font-inter-bold text-display text-primary">{strings.appName}</Text>
      </Animated.View>
      <View
        accessible
        accessibilityLabel={strings.opening.status}
        accessibilityLiveRegion="polite"
        className="h-12 justify-center"
      >
        {intro.done ? <PageTurnLoader still={intro.still} /> : null}
      </View>
    </View>
  );
}
