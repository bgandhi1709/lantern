import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { LONG_NAME } from '../test/homeFixtures';
import { SubjectPill } from './SubjectPill';

const meta = {
  title: 'Home/SubjectPill',
  component: SubjectPill,
  args: { name: 'Science', onPress: fn() },
} satisfies Meta<typeof SubjectPill>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
export const LongName: Story = { args: { name: LONG_NAME } };
