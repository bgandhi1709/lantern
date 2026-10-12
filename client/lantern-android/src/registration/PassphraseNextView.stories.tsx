import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { PassphraseNextView } from './PassphraseNextView';

const meta = {
  title: 'Registration/PassphraseNextView',
  component: PassphraseNextView,
  args: { onBack: fn() },
} satisfies Meta<typeof PassphraseNextView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
