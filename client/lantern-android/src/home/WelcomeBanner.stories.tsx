import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { LONG_NAME } from '../test/homeFixtures';
import { WelcomeBanner } from './WelcomeBanner';

const meta = {
  title: 'Home/WelcomeBanner',
  component: WelcomeBanner,
  args: { childName: 'Aarya', onDismiss: fn() },
} satisfies Meta<typeof WelcomeBanner>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
export const LongName: Story = { args: { childName: LONG_NAME } };
