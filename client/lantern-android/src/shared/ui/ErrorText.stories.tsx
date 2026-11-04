import type { Meta, StoryObj } from '@storybook/react-native';

import { en } from '../i18n/en';
import { ErrorText } from './ErrorText';

const LONG_MESSAGE = `${en.deleteCode.failures.wrongCode} ${en.deleteCode.failures.wrongCode}`;

const meta = {
  title: 'Shared/ErrorText',
  component: ErrorText,
  args: { children: en.deleteCode.failures.wrongCode },
} satisfies Meta<typeof ErrorText>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
export const Centered: Story = { args: { className: 'text-center' } };
export const LongMessage: Story = { args: { children: LONG_MESSAGE } };
