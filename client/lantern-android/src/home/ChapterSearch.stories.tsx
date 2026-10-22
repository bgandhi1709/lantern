import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { ChapterSearch } from './ChapterSearch';

const meta = {
  title: 'Home/ChapterSearch',
  component: ChapterSearch,
  args: { value: '', onChange: fn() },
} satisfies Meta<typeof ChapterSearch>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Empty: Story = {};
export const Filled: Story = { args: { value: 'number' } };
