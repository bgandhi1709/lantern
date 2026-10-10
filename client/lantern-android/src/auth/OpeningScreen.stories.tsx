import type { Meta, StoryObj } from '@storybook/react-native';

import { fixedIntro } from '../test/fixedIntro';
import { OpeningScreen } from './OpeningScreen';

const meta = { title: 'Auth/OpeningScreen', component: OpeningScreen } satisfies Meta<
  typeof OpeningScreen
>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Playing: Story = { args: { intro: fixedIntro({ done: false, still: false }) } };
export const Loading: Story = { args: { intro: fixedIntro({ done: true, still: false }) } };
export const LoadingStill: Story = { args: { intro: fixedIntro({ done: true, still: true }) } };
