import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { SchemeCard } from './SchemeCard';

const meta = {
  title: 'Settings/SchemeCard',
  component: SchemeCard,
  args: { name: 'Teal', note: 'Default. Calm.', swatch: '#0f766e', selected: false, onPick: fn() },
} satisfies Meta<typeof SchemeCard>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Unselected: Story = {};
export const Selected: Story = { args: { selected: true } };
export const LongNote: Story = {
  args: { name: 'Golden', note: 'સોનેરી અને ગરમ દીવાના પ્રકાશ જેવો રંગ', swatch: '#a16207' },
};
