import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { CLASS_THREE_SUBJECTS } from '../test/homeFixtures';
import { SubjectSheet } from './SubjectSheet';

const meta = {
  title: 'Home/SubjectSheet',
  component: SubjectSheet,
  args: {
    visible: true,
    subjects: CLASS_THREE_SUBJECTS,
    selectedId: 'english',
    onPick: fn(),
    onClose: fn(),
  },
} satisfies Meta<typeof SubjectSheet>;

export default meta;

type Story = StoryObj<typeof meta>;

export const ThreeSubjects: Story = {};
export const LastSelected: Story = { args: { selectedId: 'the-world-around-us' } };
export const TenSubjects: Story = {
  args: {
    subjects: Array.from({ length: 10 }, (_, index) => ({
      ...CLASS_THREE_SUBJECTS[index % 3],
      subjectId: `subject-${index}`,
    })),
    selectedId: 'subject-0',
  },
};
