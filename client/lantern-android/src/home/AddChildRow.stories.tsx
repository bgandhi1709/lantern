import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { AddChildRow } from './AddChildRow';

const meta = {
  title: 'Home/AddChildRow',
  component: AddChildRow,
  args: { onPress: fn() },
} satisfies Meta<typeof AddChildRow>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
