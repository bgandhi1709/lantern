import type { Meta, StoryObj } from '@storybook/react-native';
import {
  FORTY_FOUR_CHAPTERS_PROGRESS,
  NOTHING_STARTED_PROGRESS,
  SCIENCE_PROGRESS,
} from '../test/homeFixtures';
import { ChaptersSummaryCard } from './ChaptersSummaryCard';

const meta = {
  title: 'Home/ChaptersSummaryCard',
  component: ChaptersSummaryCard,
  args: { subjectName: 'Science', childName: 'Aarya', progress: SCIENCE_PROGRESS },
} satisfies Meta<typeof ChaptersSummaryCard>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Mixed: Story = {};
export const NothingStarted: Story = { args: { progress: NOTHING_STARTED_PROGRESS } };
export const FortyFourChapters: Story = {
  args: { subjectName: 'Environmental Studies', progress: FORTY_FOUR_CHAPTERS_PROGRESS },
};
