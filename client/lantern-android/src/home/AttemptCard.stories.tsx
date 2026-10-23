import type { Meta, StoryObj } from '@storybook/react-native';
import { NUMBER_PLAY_ATTEMPTS } from '../test/homeFixtures';
import { AttemptCard } from './AttemptCard';

const meta = {
  title: 'Home/AttemptCard',
  component: AttemptCard,
  args: { attempt: NUMBER_PLAY_ATTEMPTS[0] },
} satisfies Meta<typeof AttemptCard>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Planned: Story = {};
export const NoCheckYet: Story = {
  args: { attempt: { ...NUMBER_PLAY_ATTEMPTS[0], state: 'taught', conceptsDone: 5 } },
};
export const Taught: Story = {
  args: { attempt: { ...NUMBER_PLAY_ATTEMPTS[0], state: 'taught', conceptsDone: 5 } },
};
export const Checked: Story = {
  args: { attempt: { ...NUMBER_PLAY_ATTEMPTS[1], checks: [NUMBER_PLAY_ATTEMPTS[1].checks[1]] } },
};
export const CheckedWithRetry: Story = { args: { attempt: NUMBER_PLAY_ATTEMPTS[1] } };
