import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { en } from '../shared/i18n/en';
import { ClassChips } from './ClassChips';
import { classChoices } from './child';

const meta = {
  title: 'Registration/ClassChips',
  component: ClassChips,
  args: { levels: classChoices('cbse'), selected: null, onSelect: fn() },
} satisfies Meta<typeof ClassChips>;

export default meta;

type Story = StoryObj<typeof meta>;

export const NothingPicked: Story = {};
export const Picked: Story = { args: { selected: 4 } };
export const SscClasses: Story = {
  args: { levels: classChoices('ssc'), selected: 2, hint: en.registration.firstChild.sscHint },
};
