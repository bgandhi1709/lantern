import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import {
  CLASS_SIX_SUBJECTS,
  SCIENCE_CHAPTERS,
  SIX_CHILDREN,
  TWO_CHILDREN,
} from '../test/homeFixtures';
import { ChaptersView } from './ChaptersView';

const meta = {
  title: 'Home/ChaptersView',
  component: ChaptersView,
  args: {
    activeChild: { id: 'aarya', name: 'Aarya', classLevel: 6 },
    switcherChildren: TWO_CHILDREN,
    subjects: CLASS_SIX_SUBJECTS,
    subjectId: 'science',
    chapters: { status: 'ready', chapters: SCIENCE_CHAPTERS },
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
    onOpenTab: fn(),
    onTryAgain: fn(),
  },
} satisfies Meta<typeof ChaptersView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Ready: Story = {};
export const Loading: Story = { args: { chapters: { status: 'loading' } } };
export const Failed: Story = { args: { chapters: { status: 'failed' } } };
export const LoadingTheChild: Story = {
  args: { activeChild: null, subjects: [], subjectId: null, chapters: { status: 'loading' } },
};
export const SwitcherOpen: Story = { args: { sheetOpen: true } };
export const SubjectSheetOpen: Story = { args: { subjectSheetOpen: true } };
export const SixChildren: Story = {
  args: { sheetOpen: true, switcherChildren: SIX_CHILDREN, canAddChild: false },
};
