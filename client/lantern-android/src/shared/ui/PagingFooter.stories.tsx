import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { PagingFooter } from './PagingFooter';

const meta = {
  title: 'Shared/PagingFooter',
  component: PagingFooter,
  args: {
    loadingMore: true,
    failed: false,
    loadingLabel: 'Loading more Chapters…',
    failedLabel: "Can't load more Chapters",
    onRetry: fn(),
  },
} satisfies Meta<typeof PagingFooter>;

export default meta;

type Story = StoryObj<typeof meta>;

export const LoadingMore: Story = {};
export const Failed: Story = { args: { loadingMore: false, failed: true } };
