import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { PassphraseView } from './PassphraseView';

const meta = {
  title: 'Registration/PassphraseView',
  component: PassphraseView,
  args: {
    passphrase: '',
    again: '',
    understood: false,
    mismatch: false,
    canCreate: false,
    creating: false,
    failed: false,
    onPassphraseChange: fn(),
    onAgainChange: fn(),
    onUnderstoodToggle: fn(),
    onCreate: fn(),
    onBack: fn(),
  },
} satisfies Meta<typeof PassphraseView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Empty: Story = {};
export const Mismatch: Story = {
  args: { passphrase: 'blue lantern 2026', again: 'blue lantern', mismatch: true },
};
export const MatchingNotTicked: Story = {
  args: { passphrase: 'blue lantern 2026', again: 'blue lantern 2026' },
};
export const Ready: Story = {
  args: {
    passphrase: 'blue lantern 2026',
    again: 'blue lantern 2026',
    understood: true,
    canCreate: true,
  },
};
export const Creating: Story = { args: { ...Ready.args, creating: true } };
export const Failed: Story = { args: { ...Ready.args, failed: true } };
