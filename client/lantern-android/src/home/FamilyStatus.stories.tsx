import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { FamilyStatus } from './FamilyStatus';

const meta = {
  title: 'Home/FamilyStatus',
  component: FamilyStatus,
  args: { failed: false, onTryAgain: fn() },
} satisfies Meta<typeof FamilyStatus>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Loading: Story = {};
export const Failed: Story = { args: { failed: true } };
