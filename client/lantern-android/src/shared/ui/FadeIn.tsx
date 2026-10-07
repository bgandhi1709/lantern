import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { Animated, Easing } from 'react-native';

// The screen after the Opening intro settles in: a 300 ms fade and a small rise, or nothing when
// the phone asks for less motion.
export function FadeIn({ still, children }: { still: boolean; children: ReactNode }) {
  const [shown] = useState(() => new Animated.Value(still ? 1 : 0));

  useEffect(() => {
    if (still) return;
    Animated.timing(shown, {
      toValue: 1,
      duration: 300,
      easing: Easing.bezier(0.23, 1, 0.32, 1),
      useNativeDriver: true,
    }).start();
  }, [still, shown]);

  const rise = shown.interpolate({ inputRange: [0, 1], outputRange: [12, 0] });
  return (
    <Animated.View style={{ flex: 1, opacity: shown, transform: [{ translateY: rise }] }}>
      {children}
    </Animated.View>
  );
}
