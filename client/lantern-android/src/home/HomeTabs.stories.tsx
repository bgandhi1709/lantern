import type { Meta, StoryObj } from '@storybook/react-native';

import { fn } from 'storybook/test';

import { HomeTabs } from './HomeTabs';

const meta = {
  title: 'Home/HomeTabs',
  component: HomeTabs,
  args: { onOpenTab: fn() },
} satisfies Meta<typeof HomeTabs>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
