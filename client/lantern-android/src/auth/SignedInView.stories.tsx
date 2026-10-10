import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { fixedIntro } from '../test/fixedIntro';
import { SignedInPlaceholder } from './SignedInPlaceholder';
import { SignedInView } from './SignedInView';

const meta = {
  title: 'Auth/SignedInView',
  component: SignedInView,
  args: {
    status: 'checking',
    intro: fixedIntro({ done: true, still: true }),
    home: <SignedInPlaceholder />,
    onTryAgain: fn(),
    onSignOut: fn(),
  },
} satisfies Meta<typeof SignedInView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Checking: Story = {};
export const Waking: Story = { args: { status: 'waking' } };
export const NotRegistered: Story = { args: { status: 'notRegistered' } };
export const Unreachable: Story = { args: { status: 'unreachable' } };
export const Registered: Story = { args: { status: 'registered' } };
