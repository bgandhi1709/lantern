import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { birthYearChoices, emptyChild } from './child';
import { FirstChildView } from './FirstChildView';

const now = new Date('2026-10-13');
const aarav = { name: 'Aarav', classLevel: 4, birthYear: '2017', school: 'St. Mary High School' };
const mira = { name: 'Mira', classLevel: 2, birthYear: '2020', school: '' };

const meta = {
  title: 'Registration/FirstChildView',
  component: FirstChildView,
  args: {
    board: 'cbse',
    drafts: [emptyChild()],
    years: birthYearChoices(now),
    now,
    openIndex: null,
    canFinish: false,
    extraBlocks: false,
    finishing: false,
    failure: null,
    onChange: fn(),
    onToggle: fn(),
    onAdd: fn(),
    onRemove: fn(),
    onFinish: fn(),
    onBack: fn(),
  },
} satisfies Meta<typeof FirstChildView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Empty: Story = {};
export const Ready: Story = { args: { drafts: [aarav], canFinish: true } };
export const SscBoard: Story = { args: { board: 'ssc', drafts: [aarav], canFinish: true } };
export const ExtraNotStarted: Story = {
  args: { drafts: [aarav, emptyChild()], canFinish: true, openIndex: 1 },
};
export const ExtraHalfFilled: Story = {
  args: {
    drafts: [aarav, { ...mira, classLevel: null }],
    openIndex: 1,
    extraBlocks: true,
  },
};
export const ExtraReady: Story = { args: { drafts: [aarav, mira], canFinish: true } };
export const SixChildren: Story = {
  args: { drafts: [aarav, mira, mira, mira, mira, mira], canFinish: true },
};
export const Finishing: Story = { args: { drafts: [aarav], finishing: true } };
export const Unreachable: Story = {
  args: { drafts: [aarav], canFinish: true, failure: 'unreachable' },
};
export const ClassNotAvailable: Story = {
  args: { drafts: [aarav], canFinish: true, failure: 'classNotAvailable' },
};
export const SomethingWentWrong: Story = {
  args: { drafts: [aarav], canFinish: true, failure: 'other' },
};
