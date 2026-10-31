import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { SearchField } from './SearchField';

const meta = {
  title: 'Settings/SearchField',
  component: SearchField,
  args: { value: '', onChangeText: fn(), onClear: fn() },
} satisfies Meta<typeof SearchField>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Empty: Story = {};
export const Typed: Story = { args: { value: 'dark' } };
