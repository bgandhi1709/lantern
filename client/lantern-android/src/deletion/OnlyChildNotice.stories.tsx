import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { OnlyChildNotice } from './OnlyChildNotice';

const meta = {
  title: 'Deletion/OnlyChildNotice',
  component: OnlyChildNotice,
  args: { onDeleteFamily: fn() },
} satisfies Meta<typeof OnlyChildNotice>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
