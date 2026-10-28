import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { birthYearChoices, emptyChild } from '../registration';
import { AddChildView } from './AddChildView';

const mira = { name: 'Mira', classLevel: 2, birthYear: '2020', school: '' };

const meta = {
  title: 'AddChild/AddChildView',
  component: AddChildView,
  args: {
    board: 'cbse',
    child: emptyChild(),
    years: birthYearChoices(new Date('2026-10-13')),
    canAdd: false,
    adding: false,
    failure: null,
    onChange: fn(),
    onAdd: fn(),
    onBack: fn(),
  },
} satisfies Meta<typeof AddChildView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Empty: Story = {};
export const Ready: Story = { args: { child: mira, canAdd: true } };
export const SscBoard: Story = { args: { board: 'ssc', child: mira, canAdd: true } };
export const Adding: Story = { args: { child: mira, adding: true } };
export const LimitReached: Story = {
  args: { child: mira, canAdd: true, failure: 'childLimitReached' },
};
export const ClassNotAvailable: Story = {
  args: { child: mira, canAdd: true, failure: 'classNotAvailable' },
};
export const Unreachable: Story = { args: { child: mira, canAdd: true, failure: 'unreachable' } };
export const Other: Story = { args: { child: mira, canAdd: true, failure: 'other' } };
