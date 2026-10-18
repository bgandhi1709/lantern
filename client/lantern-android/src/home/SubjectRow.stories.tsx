import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { LONG_NAME } from '../test/homeFixtures';
import { SubjectRow } from './SubjectRow';

const meta = {
  title: 'Home/SubjectRow',
  component: SubjectRow,
  args: {
    subject: { subjectId: 'english', name: 'English', icon: 'english', chapterCount: 12 },
    onPress: fn(),
  },
} satisfies Meta<typeof SubjectRow>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
export const OneChapter: Story = {
  args: { subject: { subjectId: 'maths', name: 'Mathematics', icon: 'maths', chapterCount: 1 } },
};
export const LongName: Story = {
  args: { subject: { subjectId: 'evs', name: LONG_NAME, icon: 'evs', chapterCount: 25 } },
};
