import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { BoardView } from './BoardView';

const meta = {
  title: 'Registration/BoardView',
  component: BoardView,
  args: { board: null, onSelect: fn(), onContinue: fn(), onBack: fn() },
} satisfies Meta<typeof BoardView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const NothingPicked: Story = {};
export const CbsePicked: Story = { args: { board: 'cbse' } };
export const SscPicked: Story = { args: { board: 'ssc' } };
