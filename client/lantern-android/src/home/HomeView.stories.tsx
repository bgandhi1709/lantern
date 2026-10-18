import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { CLASS_THREE_SUBJECTS, SIX_CHILDREN, TWO_CHILDREN } from '../test/homeFixtures';
import { HomeView } from './HomeView';

const meta = {
  title: 'Home/HomeView',
  component: HomeView,
  args: {
    dayPart: 'evening',
    parentFirstName: 'Asha',
    locked: false,
    activeChild: { id: 'rohan', name: 'Rohan', classLevel: 3 },
    switcherChildren: TWO_CHILDREN,
    subjects: { status: 'ready', items: CLASS_THREE_SUBJECTS },
    bannerVisible: false,
    sheetOpen: false,
    canAddChild: true,
    onOpenSheet: fn(),
    onCloseSheet: fn(),
    onPickChild: fn(),
    onAddChild: fn(),
    onOpenSubject: fn(),
    onOpenSettings: fn(),
    onDismissBanner: fn(),
    onTryAgain: fn(),
    onSignOut: fn(),
  },
} satisfies Meta<typeof HomeView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
export const WithWelcomeBanner: Story = { args: { bannerVisible: true } };
export const Loading: Story = { args: { subjects: { status: 'loading' } } };
export const Failed: Story = { args: { subjects: { status: 'failed' } } };
export const SwitcherOpen: Story = { args: { sheetOpen: true } };
export const SixChildren: Story = {
  args: { sheetOpen: true, switcherChildren: SIX_CHILDREN, canAddChild: false },
};
export const Locked: Story = { args: { locked: true, activeChild: null } };
