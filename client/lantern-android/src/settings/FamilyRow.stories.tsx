import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { en } from '../shared/i18n/en';
import { FamilyRow } from './FamilyRow';

const meta = {
  title: 'Settings/FamilyRow',
  component: FamilyRow,
  args: { onPress: fn() },
} satisfies Meta<typeof FamilyRow>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Unlock: Story = { args: en.appearance.unlockRow };

export const StartAgain: Story = { args: en.appearance.startAgainRow };

export const DeleteChild: Story = { args: { ...en.settings.deleteChildRow, danger: true } };

export const DeleteFamily: Story = { args: { ...en.settings.deleteFamilyRow, danger: true } };
