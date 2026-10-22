import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import {
  CLASS_SIX_SUBJECTS,
  SCIENCE_CHAPTERS,
  SCIENCE_PROGRESS,
  SIX_CHILDREN,
  TWO_CHILDREN,
} from '../test/homeFixtures';
import { ChaptersView } from './ChaptersView';

const READY = {
  status: 'ready',
  items: SCIENCE_CHAPTERS,
  progress: SCIENCE_PROGRESS,
  refreshing: false,
  loadingMore: false,
  moreFailed: false,
} as const;

const meta = {
  title: 'Home/ChaptersView',
  component: ChaptersView,
  args: {
    activeChild: { id: 'aarya', name: 'Aarya', classLevel: 6 },
    switcherChildren: TWO_CHILDREN,
    subjects: CLASS_SIX_SUBJECTS,
    subjectId: 'science',
    chapters: READY,
    query: '',
    sheetOpen: false,
    subjectSheetOpen: false,
    canAddChild: true,
    onBack: fn(),
    onOpenSheet: fn(),
    onOpenSubjectSheet: fn(),
    onCloseSubjectSheet: fn(),
    onCloseSheet: fn(),
    onPickChild: fn(),
    onAddChild: fn(),
    onSelectSubject: fn(),
    onChangeQuery: fn(),
    onAction: fn(),
    onOpenHistory: fn(),
    onLoadMore: fn(),
    onRetryMore: fn(),
    onOpenTab: fn(),
    onTryAgain: fn(),
  },
} satisfies Meta<typeof ChaptersView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Ready: Story = {};
export const Loading: Story = {
  args: { chapters: { ...READY, status: 'loading', items: [], progress: null } },
};
export const Failed: Story = {
  args: { chapters: { ...READY, status: 'failed', items: [], progress: null } },
};
export const LoadingTheChild: Story = {
  args: {
    activeChild: null,
    subjects: [],
    subjectId: null,
    chapters: { ...READY, status: 'loading', items: [], progress: null },
  },
};
export const Searching: Story = {
  args: { query: 'state', chapters: { ...READY, items: [SCIENCE_CHAPTERS[1]], refreshing: true } },
};
export const NoMatch: Story = {
  args: { query: 'zebra', chapters: { ...READY, items: [] } },
};
export const LoadingMore: Story = { args: { chapters: { ...READY, loadingMore: true } } };
export const LoadMoreFailed: Story = { args: { chapters: { ...READY, moreFailed: true } } };
export const SwitcherOpen: Story = { args: { sheetOpen: true } };
export const SubjectSheetOpen: Story = { args: { subjectSheetOpen: true } };
export const SixChildren: Story = {
  args: { sheetOpen: true, switcherChildren: SIX_CHILDREN, canAddChild: false },
};
