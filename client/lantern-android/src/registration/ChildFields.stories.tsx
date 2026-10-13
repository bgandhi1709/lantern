import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { birthYearChoices, emptyChild } from './child';
import { ChildFields } from './ChildFields';

const meta = {
  title: 'Registration/ChildFields',
  component: ChildFields,
  args: {
    child: emptyChild(),
    board: 'cbse',
    years: birthYearChoices(new Date('2026-10-13')),
    idPrefix: 'story',
    onChange: fn(),
  },
} satisfies Meta<typeof ChildFields>;

export default meta;

type Story = StoryObj<typeof meta>;

const filled = { name: 'Aarav', classLevel: 4, birthYear: '2017', school: 'St. Mary High School' };

export const Empty: Story = {};
export const Filled: Story = { args: { child: filled } };
export const ClassAsList: Story = { args: { child: filled, classAsList: true } };
export const SscBoard: Story = { args: { child: filled, board: 'ssc' } };
export const LongName: Story = {
  args: {
    child: { ...filled, name: 'Venkatasubramanian Padmanabhan Iyer', school: 'S'.repeat(90) },
  },
};
