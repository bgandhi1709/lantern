import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { Animated } from 'react-native';

import { motion } from './theme';

// The screen after the Opening intro settles in: a 300 ms fade and a small rise. When the phone
// asks for less motion it only fades, which still shows that the screen changed.
export function FadeIn({ still, children }: { still: boolean; children: ReactNode }) {
  const [shown] = useState(() => new Animated.Value(0));

  useEffect(() => {
    const animation = Animated.timing(shown, {
      toValue: 1,
      duration: still ? 200 : 300,
      easing: motion.easeOut,
      useNativeDriver: true,
    });
    animation.start();
    return () => animation.stop();
  }, [still, shown]);

  const rise = shown.interpolate({ inputRange: [0, 1], outputRange: [still ? 0 : 12, 0] });
  return (
    <Animated.View style={{ flex: 1, opacity: shown, transform: [{ translateY: rise }] }}>
      {children}
    </Animated.View>
  );
}
