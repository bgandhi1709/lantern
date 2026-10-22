import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { UnlockView } from './UnlockView';

const meta = {
  title: 'Unlock/UnlockView',
  component: UnlockView,
  args: {
    phase: 'ready',
    value: '',
    opening: false,
    wrong: false,
    somethingWentWrong: false,
    secondsLeft: 0,
    onChange: fn(),
    onOpen: fn(),
    onLoadAgain: fn(),
    onBack: fn(),
  },
} satisfies Meta<typeof UnlockView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Typing: Story = { args: { value: 'K7QM-4TDX' } };

export const Opening: Story = { args: { value: 'K7QM-4TDX-92HB-WEPN', opening: true } };

export const WrongCode: Story = { args: { value: 'K7QM-4TDX', wrong: true } };

export const WaitingAfterWrongTry: Story = {
  args: { value: 'K7QM-4TDX', wrong: true, secondsLeft: 20 },
};

export const LoadingFamily: Story = { args: { phase: 'loading' } };

export const CannotReachLantern: Story = { args: { phase: 'failed' } };

export const SomethingWentWrong: Story = {
  args: { value: 'K7QM-4TDX-92HB-WEPN', somethingWentWrong: true },
};
