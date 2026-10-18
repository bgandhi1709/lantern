import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { LONG_NAME } from '../test/homeFixtures';
import { ChildSwitcherButton } from './ChildSwitcherButton';

const meta = {
  title: 'Home/ChildSwitcherButton',
  component: ChildSwitcherButton,
  args: { name: 'Aarya', onPress: fn() },
} satisfies Meta<typeof ChildSwitcherButton>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
export const LongName: Story = { args: { name: LONG_NAME } };
