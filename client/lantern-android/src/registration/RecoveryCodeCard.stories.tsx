import type { Meta, StoryObj } from '@storybook/react-native';

import { RecoveryCodeCard } from './RecoveryCodeCard';

const meta = {
  title: 'Registration/RecoveryCodeCard',
  component: RecoveryCodeCard,
  args: { code: 'K7QM-4TDX-92HB-WEPN' },
} satisfies Meta<typeof RecoveryCodeCard>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
// The widest letters, to show the code still fits one line on a narrow phone.
export const WidestLetters: Story = { args: { code: 'WWWW-MMMM-WWWW-MMMM' } };
