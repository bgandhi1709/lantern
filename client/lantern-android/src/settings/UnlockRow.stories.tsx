import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { UnlockRow } from './UnlockRow';

const meta = {
  title: 'Settings/UnlockRow',
  component: UnlockRow,
  args: { onPress: fn() },
} satisfies Meta<typeof UnlockRow>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
