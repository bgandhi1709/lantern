import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { GoogleButton } from './GoogleButton';

const meta = {
  title: 'Auth/GoogleButton',
  component: GoogleButton,
  args: { busy: false, onPress: fn() },
} satisfies Meta<typeof GoogleButton>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Idle: Story = {};
export const Busy: Story = { args: { busy: true } };
