import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { StartView } from './StartView';

const meta = {
  title: 'Auth/StartView',
  component: StartView,
  args: { status: 'idle', onSignIn: fn() },
} satisfies Meta<typeof StartView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Idle: Story = {};
export const SigningIn: Story = { args: { status: 'signingIn' } };
export const Failed: Story = { args: { status: 'failed' } };
