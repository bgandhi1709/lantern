import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { UnlockNotice } from './UnlockNotice';

const meta = {
  title: 'Home/UnlockNotice',
  component: UnlockNotice,
  args: { onUnlock: fn(), onSignOut: fn() },
} satisfies Meta<typeof UnlockNotice>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
