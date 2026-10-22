import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { UnlockView } from './UnlockView';

const meta = {
  title: 'Unlock/UnlockView',
  component: UnlockView,
  args: {
    phase: 'ready',
    kind: 'passphrase',
    value: '',
    opening: false,
    wrong: false,
    somethingWentWrong: false,
    secondsLeft: 0,
    onKind: fn(),
    onChange: fn(),
    onOpen: fn(),
    onLoadAgain: fn(),
    onStartAgain: fn(),
    onBack: fn(),
  },
} satisfies Meta<typeof UnlockView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Passphrase: Story = { args: { value: 'blue lantern' } };

export const RecoveryCode: Story = { args: { kind: 'recoveryCode', value: 'K7QM-4TDX' } };

export const Opening: Story = { args: { value: 'blue lantern', opening: true } };

export const WrongPassphrase: Story = { args: { value: 'blue lantern', wrong: true } };

export const WaitingAfterWrongTry: Story = {
  args: { kind: 'recoveryCode', value: 'K7QM-4TDX', wrong: true, secondsLeft: 20 },
};

export const LoadingFamily: Story = { args: { phase: 'loading' } };

export const CannotReachLantern: Story = { args: { phase: 'failed' } };

export const SomethingWentWrong: Story = {
  args: { value: 'blue lantern', somethingWentWrong: true },
};
