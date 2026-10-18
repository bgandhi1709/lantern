import type { Meta, StoryObj } from '@storybook/react-native';

import { ChildAvatar } from './ChildAvatar';

const meta = {
  title: 'Home/ChildAvatar',
  component: ChildAvatar,
  args: { name: 'Aarya' },
} satisfies Meta<typeof ChildAvatar>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Small: Story = {};
export const Large: Story = { args: { large: true } };
