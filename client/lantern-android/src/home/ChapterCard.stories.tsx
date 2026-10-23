import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { SCIENCE_CHAPTERS } from '../test/homeFixtures';
import { ChapterCard } from './ChapterCard';

const meta = {
  title: 'Home/ChapterCard',
  component: ChapterCard,
  args: { chapter: SCIENCE_CHAPTERS[2], onAction: fn(), onOpenHistory: fn() },
} satisfies Meta<typeof ChapterCard>;

export default meta;

type Story = StoryObj<typeof meta>;

export const NotStarted: Story = {};
export const Planned: Story = { args: { chapter: SCIENCE_CHAPTERS[1] } };
export const SecondAttempt: Story = {
  args: { chapter: { ...SCIENCE_CHAPTERS[1], attemptCount: 2, conceptsDone: 2, conceptCount: 5 } },
};
export const Taught: Story = {
  args: { chapter: { ...SCIENCE_CHAPTERS[1], state: 'taught', conceptsDone: 4 } },
};
export const Checked: Story = { args: { chapter: SCIENCE_CHAPTERS[0] } };
export const LongTitle: Story = {
  args: {
    chapter: {
      ...SCIENCE_CHAPTERS[2],
      title: 'Measurement of Length and Motion in Everyday Life and in the Laboratory',
    },
  },
};
export const LongSummary: Story = {
  args: {
    chapter: {
      ...SCIENCE_CHAPTERS[1],
      summary: `${SCIENCE_CHAPTERS[1].summary} ${SCIENCE_CHAPTERS[0].summary} ${SCIENCE_CHAPTERS[2].summary}`,
    },
  },
};
