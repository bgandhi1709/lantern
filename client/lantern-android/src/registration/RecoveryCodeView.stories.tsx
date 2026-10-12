import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { RecoveryCodeView } from './RecoveryCodeView';

const meta = {
  title: 'Registration/RecoveryCodeView',
  component: RecoveryCodeView,
  args: {
    code: 'K7QM-4TDX-92HB-WEPN',
    saved: false,
    onSavedToggle: fn(),
    onEmail: fn(),
    onSave: fn(),
    onContinue: fn(),
  },
} satisfies Meta<typeof RecoveryCodeView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const NotSavedYet: Story = {};
export const ShareFailed: Story = { args: { shareFailed: true } };
export const Saved: Story = { args: { saved: true } };
