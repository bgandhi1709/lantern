import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { NUMBER_PLAY_ATTEMPTS, NUMBER_PLAY_HEADER } from '../test/homeFixtures';
import { ChapterHistoryView } from './ChapterHistoryView';

const meta = {
  title: 'Home/ChapterHistoryView',
  component: ChapterHistoryView,
  args: {
    history: {
      status: 'ready',
      items: NUMBER_PLAY_ATTEMPTS,
      meta: NUMBER_PLAY_HEADER,
      loadingMore: false,
      moreFailed: false,
    },
    onBack: fn(),
    onAction: fn(),
    onLoadMore: fn(),
    onRetryMore: fn(),
    onTryAgain: fn(),
  },
} satisfies Meta<typeof ChapterHistoryView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Ready: Story = {};
export const Loading: Story = {
  args: {
    history: { status: 'loading', items: [], meta: null, loadingMore: false, moreFailed: false },
  },
};
export const Failed: Story = {
  args: {
    history: { status: 'failed', items: [], meta: null, loadingMore: false, moreFailed: false },
  },
};
export const NoAttempts: Story = {
  args: {
    history: {
      status: 'ready',
      items: [],
      meta: {
        ...NUMBER_PLAY_HEADER,
        state: 'not-started',
        attemptCount: 0,
        checkCount: 0,
        conceptsDone: null,
        conceptCount: null,
      },
      loadingMore: false,
      moreFailed: false,
    },
  },
};
export const LoadingMore: Story = {
  args: {
    history: {
      status: 'ready',
      items: NUMBER_PLAY_ATTEMPTS,
      meta: NUMBER_PLAY_HEADER,
      loadingMore: true,
      moreFailed: false,
    },
  },
};
export const LoadMoreFailed: Story = {
  args: {
    history: {
      status: 'ready',
      items: NUMBER_PLAY_ATTEMPTS,
      meta: NUMBER_PLAY_HEADER,
      loadingMore: false,
      moreFailed: true,
    },
  },
};
