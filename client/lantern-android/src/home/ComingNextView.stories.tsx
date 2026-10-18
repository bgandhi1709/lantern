import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { ComingNextView } from './ComingNextView';

const meta = {
  title: 'Home/ComingNextView',
  component: ComingNextView,
  args: { onBack: fn() },
} satisfies Meta<typeof ComingNextView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
