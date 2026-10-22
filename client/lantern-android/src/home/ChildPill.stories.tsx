import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { LONG_NAME } from '../test/homeFixtures';
import { ChildPill } from './ChildPill';

const meta = {
  title: 'Home/ChildPill',
  component: ChildPill,
  args: { name: 'Aarya', onPress: fn() },
} satisfies Meta<typeof ChildPill>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
export const LongName: Story = { args: { name: LONG_NAME } };
