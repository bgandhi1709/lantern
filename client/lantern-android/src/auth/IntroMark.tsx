import type { ReactNode } from 'react';
import { Animated, StyleSheet, View } from 'react-native';
import Svg from 'react-native-svg';

import { useStrings } from '../shared/i18n';
import { Arch, Book, Child, Hands, Light, Mother, Star } from '../shared/ui/markParts';
import type { IntroPart } from './intro';
import type { IntroValues } from './useIntro';

const VIEWBOX = 120;

// Each part enters from a small offset, in the mark's own units, so motion scales with its size.
const enter: Record<Exclude<IntroPart, 'word'>, { x?: number; y?: number; scale?: number }> = {
  book: { scale: 0.6 },
  mother: { y: 10 },
  child: { y: 10 },
  hands: { x: -6, y: -6 },
  light: { y: 8 },
  arch: {},
  star: { scale: 0.6 },
};

function Layer({
  value,
  part,
  unit,
  children,
}: {
  value: Animated.Value;
  part: keyof typeof enter;
  unit: number;
  children: ReactNode;
}) {
  const { x = 0, y = 0, scale = 1 } = enter[part];
  const from = (start: number) =>
    value.interpolate({ inputRange: [0, 1], outputRange: [start, 0] });
  const grow = value.interpolate({ inputRange: [0, 1], outputRange: [scale, 1] });

  return (
    <Animated.View
      style={[
        StyleSheet.absoluteFill,
        {
          opacity: value,
          transform: [
            { translateX: from(x * unit) },
            { translateY: from(y * unit) },
            { scale: grow },
          ],
        },
      ]}
    >
      <Svg width="100%" height="100%" viewBox={`0 0 ${VIEWBOX} ${VIEWBOX}`}>
        {children}
      </Svg>
    </Animated.View>
  );
}

export function IntroMark({ values, size }: { values: IntroValues; size: number }) {
  const label = useStrings().mark.label;
  const unit = size / VIEWBOX;
  const layer = (part: keyof typeof enter, children: ReactNode) => (
    <Layer value={values[part]} part={part} unit={unit}>
      {children}
    </Layer>
  );

  return (
    <View
      accessible
      accessibilityRole="image"
      accessibilityLabel={label}
      style={{ width: size, height: size }}
    >
      {layer('arch', <Arch />)}
      {layer('mother', <Mother />)}
      {layer('child', <Child />)}
      {layer('book', <Book />)}
      {layer('light', <Light />)}
      {layer('hands', <Hands />)}
      {layer('star', <Star />)}
    </View>
  );
}
