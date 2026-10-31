import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { TWO_CHILDREN } from '../test/homeFixtures';
import { ChildChoices } from './ChildChoices';

const meta = {
  title: 'Deletion/ChildChoices',
  component: ChildChoices,
  args: { options: TWO_CHILDREN, pickedId: null, onPick: fn() },
} satisfies Meta<typeof ChildChoices>;

export default meta;

type Story = StoryObj<typeof meta>;

export const NothingPicked: Story = {};
export const Picked: Story = { args: { pickedId: 'rohan' } };
export const LongName: Story = {
  args: { options: [{ id: 'long', name: 'Venkatasubramanian Raghunathan', classLevel: 6 }] },
};
