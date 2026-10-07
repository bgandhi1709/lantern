import { useEffect, useState } from 'react';
import { Animated, Easing, StyleSheet, View } from 'react-native';
import Svg, { Path } from 'react-native-svg';

import { colors, markColors } from '../shared/ui/theme';

const WIDTH = 72;
const HEIGHT = 48;
const LEFT_PAGE = 'M24 9 Q14 5 4 8 V28 Q14 25 24 29 Z';
const RIGHT_PAGE = 'M24 9 Q34 5 44 8 V28 Q34 25 24 29 Z';
const TURNING_PAGE = 'M0 9 Q10 5 20 8 V28 Q10 25 0 29 Z';
const SPARK = 'M24 0 L25 2.6 L27.6 3.6 L25 4.6 L24 7.2 L23 4.6 L20.4 3.6 L23 2.6 Z';

const Page = ({ d, fill }: { d: string; fill: string }) => (
  <Path d={d} fill={fill} stroke={colors.primary} strokeWidth={1.6} strokeLinejoin="round" />
);

// An open book whose right page keeps turning over to the left, shown while Lantern is still opening.
export function PageTurnLoader({ still }: { still: boolean }) {
  const [turn] = useState(() => new Animated.Value(0));

  useEffect(() => {
    if (still) return;
    const loop = Animated.loop(
      Animated.timing(turn, {
        toValue: 1,
        duration: 1400,
        easing: Easing.linear,
        useNativeDriver: true,
      }),
    );
    loop.start();
    return () => loop.stop();
  }, [still, turn]);

  const scaleX = turn.interpolate({
    inputRange: [0, 0.45, 0.55, 0.56, 1],
    outputRange: [1, -1, -1, 1, 1],
  });
  const opacity = turn.interpolate({
    inputRange: [0, 0.45, 0.55, 0.56, 0.7, 1],
    outputRange: [1, 1, 0, 0, 1, 1],
  });

  return (
    <View style={styles.book}>
      <Svg width={WIDTH} height={HEIGHT} viewBox="0 0 48 32" style={StyleSheet.absoluteFill}>
        <Page d={LEFT_PAGE} fill={colors.iconTile} />
        <Page d={RIGHT_PAGE} fill={colors.iconTile} />
        <Path d={SPARK} fill={markColors.light} />
      </Svg>
      <Animated.View style={[styles.turningPage, { opacity, transform: [{ scaleX }] }]}>
        <Svg width={WIDTH / 2} height={HEIGHT} viewBox="0 0 24 32">
          <Page d={TURNING_PAGE} fill={colors.card} />
        </Svg>
      </Animated.View>
    </View>
  );
}

const styles = StyleSheet.create({
  book: { width: WIDTH, height: HEIGHT },
  turningPage: { position: 'absolute', left: WIDTH / 2, top: 0, transformOrigin: 'left' },
});
