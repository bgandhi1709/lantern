import type { Meta, StoryObj } from '@storybook/react-native';

import { GoogleAccountCard } from './GoogleAccountCard';

const meta = {
  title: 'Registration/GoogleAccountCard',
  component: GoogleAccountCard,
  args: { name: 'Asha Verma', email: 'asha.verma@gmail.com' },
} satisfies Meta<typeof GoogleAccountCard>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
// A long name and address must wrap inside the card, not run off the screen.
export const LongValues: Story = {
  args: {
    name: 'Aashirwadkumari Venkatasubramanian Iyer-Chaturvedi',
    email: 'aashirwadkumari.venkatasubramanian.iyer.chaturvedi@example.com',
  },
};
