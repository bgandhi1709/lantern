import { Animated } from 'react-native';

import type { Intro } from '../auth/useIntro';

// A finished intro with fixed values, so a story shows the mark and name without waiting for the animation.
export const fixedIntro = (flags: { done: boolean; still: boolean }): Intro => ({
  values: {
    book: new Animated.Value(1),
    mother: new Animated.Value(1),
    child: new Animated.Value(1),
    hands: new Animated.Value(1),
    light: new Animated.Value(1),
    arch: new Animated.Value(1),
    star: new Animated.Value(1),
    word: new Animated.Value(1),
  },
  ...flags,
});
