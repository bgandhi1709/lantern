import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { HomeHeader } from './HomeHeader';

const meta = {
  title: 'Home/HomeHeader',
  component: HomeHeader,
  args: { onOpenSettings: fn(), onSignOut: fn() },
} satisfies Meta<typeof HomeHeader>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
