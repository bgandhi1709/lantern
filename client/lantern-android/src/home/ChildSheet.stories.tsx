import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { SIX_CHILDREN, TWO_CHILDREN } from '../test/homeFixtures';
import { ChildSheet } from './ChildSheet';

const meta = {
  title: 'Home/ChildSheet',
  component: ChildSheet,
  args: {
    visible: true,
    switcherChildren: TWO_CHILDREN,
    activeId: 'aarya',
    onPick: fn(),
    onClose: fn(),
    onAddChild: fn(),
  },
} satisfies Meta<typeof ChildSheet>;

export default meta;

type Story = StoryObj<typeof meta>;

export const TwoChildren: Story = {};
export const OneStillBeingMade: Story = {
  args: {
    switcherChildren: [TWO_CHILDREN[0], { ...TWO_CHILDREN[1], stillBeingMade: true }],
  },
};
export const SixChildren: Story = {
  args: { switcherChildren: SIX_CHILDREN, onAddChild: undefined },
};
