import type { Meta, StoryObj } from '@storybook/react-native';
import type { ComponentProps } from 'react';
import { Text } from 'react-native';
import { fn } from 'storybook/test';

import { en } from '../shared/i18n/en';
import { fixedIntro } from '../test/fixedIntro';
import { SignedInView } from './SignedInView';

// Home and registration are nodes, and Storybook's args must stay plain data (it walks into a node's
// internals and fails on React Navigation's context), so the story fixes them. Auth never imports the
// registration flow, so a heading stands in for it, and the same for Home.
function SignedInViewWithScreens(
  props: Omit<ComponentProps<typeof SignedInView>, 'home' | 'registration'>,
) {
  return (
    <SignedInView
      {...props}
      home={<Text>{en.home.greeting.morning('Asha')}</Text>}
      registration={<Text>{en.registration.about.heading}</Text>}
    />
  );
}

const meta = {
  title: 'Auth/SignedInView',
  component: SignedInViewWithScreens,
  args: {
    status: 'checking',
    intro: fixedIntro({ done: true, still: true }),
    onTryAgain: fn(),
  },
} satisfies Meta<typeof SignedInViewWithScreens>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Checking: Story = {};
export const Waking: Story = { args: { status: 'waking' } };
export const NotRegistered: Story = { args: { status: 'notRegistered' } };
export const Unreachable: Story = { args: { status: 'unreachable' } };
export const Registered: Story = { args: { status: 'registered' } };
