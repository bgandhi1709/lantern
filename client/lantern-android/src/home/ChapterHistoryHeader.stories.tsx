import type { Meta, StoryObj } from '@storybook/react-native';
import { NUMBER_PLAY_HEADER } from '../test/homeFixtures';
import { ChapterHistoryHeader } from './ChapterHistoryHeader';

const meta = {
  title: 'Home/ChapterHistoryHeader',
  component: ChapterHistoryHeader,
  args: { chapter: NUMBER_PLAY_HEADER },
} satisfies Meta<typeof ChapterHistoryHeader>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Planned: Story = {};
export const Checked: Story = {
  args: {
    chapter: {
      ...NUMBER_PLAY_HEADER,
      state: 'checked',
      conceptsDone: 5,
      attemptCount: 1,
      checkCount: 2,
    },
  },
};
