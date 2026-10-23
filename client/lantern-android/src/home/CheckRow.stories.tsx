import type { Meta, StoryObj } from '@storybook/react-native';
import { NUMBER_PLAY_ATTEMPTS } from '../test/homeFixtures';
import { CheckRow } from './CheckRow';

const meta = {
  title: 'Home/CheckRow',
  component: CheckRow,
  args: { check: NUMBER_PLAY_ATTEMPTS[1].checks[1] },
} satisfies Meta<typeof CheckRow>;

export default meta;

type Story = StoryObj<typeof meta>;

export const NeedsPractice: Story = {};
export const Strong: Story = { args: { check: NUMBER_PLAY_ATTEMPTS[1].checks[0] } };
export const AllRight: Story = {
  args: { check: { ...NUMBER_PLAY_ATTEMPTS[1].checks[0], right: 5, wrong: [] } },
};
export const LongConceptNames: Story = {
  args: {
    check: {
      ...NUMBER_PLAY_ATTEMPTS[1].checks[1],
      wrong: [
        { conceptId: 'a', title: 'Reading and writing numbers up to 9999 in words and in figures' },
        { conceptId: 'b', title: 'Comparing and ordering numbers using place value' },
      ],
    },
  },
};
