import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { PrimaryButton } from './PrimaryButton';

const meta = {
  title: 'Shared/PrimaryButton',
  component: PrimaryButton,
  args: { label: 'Sign out', onPress: fn() },
} satisfies Meta<typeof PrimaryButton>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
// Gujarati and Hindi labels run longer than English, so the button must still hold them.
export const LongLabel: Story = {
  args: { label: 'તમારા બાળકના પુસ્તકનું પહેલું પાનું ફોટોગ્રાફ કરો અને આગળ વધો' },
};
