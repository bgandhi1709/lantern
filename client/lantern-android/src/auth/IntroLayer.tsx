import type { ReactNode } from 'react';
import { Animated, StyleSheet } from 'react-native';
import Svg from 'react-native-svg';

import { MARK_GRID, type MarkPart } from '../shared/ui/markParts';

// Each part enters from a small offset, in the mark's own units, so motion scales with its size.
const enter: Record<MarkPart, { x?: number; y?: number; scale?: number }> = {
  book: { scale: 0.95 },
  mother: { y: 10 },
  child: { y: 10 },
  hands: { x: -6, y: -6 },
  light: { y: 8 },
  arch: {},
  star: { scale: 0.95 },
};

export function IntroLayer({
  value,
  part,
  unit,
  children,
}: {
  value: Animated.Value;
  part: MarkPart;
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
      <Svg width="100%" height="100%" viewBox={`0 0 ${MARK_GRID} ${MARK_GRID}`}>
        {children}
      </Svg>
    </Animated.View>
  );
}
