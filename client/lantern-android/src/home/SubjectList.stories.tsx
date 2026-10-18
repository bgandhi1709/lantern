import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { CLASS_THREE_SUBJECTS } from '../test/homeFixtures';
import { SubjectList } from './SubjectList';

const meta = {
  title: 'Home/SubjectList',
  component: SubjectList,
  args: {
    childName: 'Rohan',
    classLevel: 3,
    state: { status: 'ready', items: CLASS_THREE_SUBJECTS },
    onOpenSubject: fn(),
    onTryAgain: fn(),
  },
} satisfies Meta<typeof SubjectList>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Ready: Story = {};
export const Loading: Story = { args: { state: { status: 'loading' } } };
export const Failed: Story = { args: { state: { status: 'failed' } } };
