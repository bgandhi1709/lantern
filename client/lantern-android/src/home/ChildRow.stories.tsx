import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { LONG_NAME } from '../test/homeFixtures';
import { ChildRow } from './ChildRow';

const meta = {
  title: 'Home/ChildRow',
  component: ChildRow,
  args: {
    child: { id: 'aarya', name: 'Aarya', classLevel: 6, stillBeingMade: false },
    selected: false,
    onPick: fn(),
  },
} satisfies Meta<typeof ChildRow>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Unselected: Story = {};
export const Selected: Story = { args: { selected: true } };
export const GettingReady: Story = {
  args: { child: { id: 'rohan', name: 'Rohan', classLevel: 3, stillBeingMade: true } },
};
export const LongName: Story = {
  args: {
    child: { id: 'long', name: LONG_NAME, classLevel: 10, stillBeingMade: false },
    selected: true,
  },
};
