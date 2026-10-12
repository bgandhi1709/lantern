import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { BoardNextView } from './BoardNextView';

const meta = {
  title: 'Registration/BoardNextView',
  component: BoardNextView,
  args: { onBack: fn() },
} satisfies Meta<typeof BoardNextView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
