import type { Meta, StoryObj } from '@storybook/react-native';
import { Animated } from 'react-native';

import { OpeningScreen } from './OpeningScreen';
import type { Intro } from './useIntro';

// Fixed values stand in for the intro animation, shown as it ends (1) so a reviewer sees the mark and name.
const introEnd = (flags: { done: boolean; still: boolean }): Intro => ({
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

const meta = { title: 'Auth/OpeningScreen', component: OpeningScreen } satisfies Meta<
  typeof OpeningScreen
>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Playing: Story = { args: { intro: introEnd({ done: false, still: false }) } };
export const Loading: Story = { args: { intro: introEnd({ done: true, still: false }) } };
export const LoadingStill: Story = { args: { intro: introEnd({ done: true, still: true }) } };
