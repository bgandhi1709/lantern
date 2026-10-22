import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { CLASS_THREE_SUBJECTS, LONG_NAME } from '../test/homeFixtures';
import { SubjectOption } from './SubjectOption';

const meta = {
  title: 'Home/SubjectOption',
  component: SubjectOption,
  args: { subject: CLASS_THREE_SUBJECTS[0], selected: false, onPick: fn() },
} satisfies Meta<typeof SubjectOption>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Unselected: Story = {};
export const Selected: Story = { args: { selected: true } };
export const OneChapter: Story = {
  args: { subject: { ...CLASS_THREE_SUBJECTS[1], chapterCount: 1 } },
};
export const LongName: Story = {
  args: { subject: { ...CLASS_THREE_SUBJECTS[2], name: LONG_NAME }, selected: true },
};
