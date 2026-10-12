import type { Meta, StoryObj } from '@storybook/react-native';
import { View } from 'react-native';
import { fn } from 'storybook/test';

import { en } from '../shared/i18n/en';
import { BoardCard } from './BoardCard';

const meta = {
  title: 'Registration/BoardCard',
  component: BoardCard,
  args: { board: 'cbse', ...en.registration.board.cards.cbse, selected: false, onSelect: fn() },
  decorators: [
    (Story) => (
      <View className="w-44 flex-row p-4">
        <Story />
      </View>
    ),
  ],
} satisfies Meta<typeof BoardCard>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Unselected: Story = {};
export const Selected: Story = { args: { selected: true } };
export const LongLabel: Story = {
  args: {
    board: 'ssc',
    title: 'મહારાષ્ટ્ર રાજ્ય શિક્ષણ બોર્ડ',
    subtitle: 'SSC · બાલભારતી પુસ્તકો, અંગ્રેજી માધ્યમ',
    classes: 'ધોરણ 1 થી 5',
    selected: true,
  },
};
