import type { Meta, StoryObj } from '@storybook/react-native';

import { SCIENCE_CHAPTERS } from '../test/homeFixtures';
import { ChapterCard } from './ChapterCard';

const meta = {
  title: 'Home/ChapterCard',
  component: ChapterCard,
  args: { chapter: SCIENCE_CHAPTERS[0] },
} satisfies Meta<typeof ChapterCard>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
export const LongTitle: Story = { args: { chapter: SCIENCE_CHAPTERS[2] } };
export const TwoDigitNumber: Story = { args: { chapter: { ...SCIENCE_CHAPTERS[0], number: 14 } } };
export const LongSummary: Story = {
  args: {
    chapter: {
      ...SCIENCE_CHAPTERS[1],
      summary: `${SCIENCE_CHAPTERS[1].summary} ${SCIENCE_CHAPTERS[0].summary} ${SCIENCE_CHAPTERS[2].summary}`,
    },
  },
};
