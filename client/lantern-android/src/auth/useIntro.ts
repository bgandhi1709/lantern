import { useEffect, useState } from 'react';
import { AccessibilityInfo, Animated } from 'react-native';

import { readValue, writeValue } from '../shared/storage';
import { motion } from '../shared/ui/theme';
import { type IntroKind, type IntroPart, introKind, introTimeline } from './intro';

export type IntroValues = Record<IntroPart, Animated.Value>;
export type Intro = { values: IntroValues; done: boolean; still: boolean };

const makeValues = (): IntroValues => ({
  book: new Animated.Value(0),
  mother: new Animated.Value(0),
  child: new Animated.Value(0),
  hands: new Animated.Value(0),
  light: new Animated.Value(0),
  arch: new Animated.Value(0),
  star: new Animated.Value(0),
  word: new Animated.Value(0),
});

async function chooseKind(): Promise<IntroKind> {
  const [seen, reduceMotion] = await Promise.all([
    readValue('introSeen'),
    AccessibilityInfo.isReduceMotionEnabled().catch(() => false),
  ]);
  return introKind({ seen, reduceMotion });
}

// Plays the Opening intro once per mount, on the native driver so it keeps running while the app
// restores the session. done turns true when the last part is in place.
export function useIntro(): Intro {
  const [values] = useState(makeValues);
  const [kind, setKind] = useState<IntroKind | null>(null);
  const [done, setDone] = useState(false);

  useEffect(() => {
    let alive = true;
    chooseKind().then((chosen) => alive && setKind(chosen));
    return () => {
      alive = false;
    };
  }, []);

  useEffect(() => {
    if (kind === null) return;
    const finish = () => {
      setDone(true);
      writeValue('introSeen', true).catch(() => undefined);
    };
    if (kind === 'still') {
      Object.values(values).forEach((value) => value.setValue(1));
      finish();
      return;
    }
    const animation = Animated.parallel(
      introTimeline(kind).map(({ part, delay, duration }) =>
        Animated.timing(values[part], {
          toValue: 1,
          delay,
          duration,
          easing: motion.easeOut,
          useNativeDriver: true,
        }),
      ),
    );
    animation.start(({ finished }) => finished && finish());
    return () => animation.stop();
  }, [kind, values]);

  return { values, done, still: kind === 'still' };
}
