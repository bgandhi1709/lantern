import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { StartAgainView } from './StartAgainView';

const meta = {
  title: 'Unlock/StartAgainView',
  component: StartAgainView,
  args: { deleting: false, failed: false, onDelete: fn(), onKeep: fn() },
} satisfies Meta<typeof StartAgainView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};

export const Deleting: Story = { args: { deleting: true } };

export const Failed: Story = { args: { failed: true } };
