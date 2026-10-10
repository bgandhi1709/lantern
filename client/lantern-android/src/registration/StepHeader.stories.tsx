import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { StepHeader } from './StepHeader';

const meta = {
  title: 'Registration/StepHeader',
  component: StepHeader,
  args: { step: 1, total: 4, onBack: fn() },
} satisfies Meta<typeof StepHeader>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Step1: Story = {};
export const Step2: Story = { args: { step: 2 } };
export const Step3: Story = { args: { step: 3 } };
export const Step4: Story = { args: { step: 4 } };
