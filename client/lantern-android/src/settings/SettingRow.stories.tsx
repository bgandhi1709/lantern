import type { Meta, StoryObj } from '@storybook/react-native';
import { Text } from 'react-native';
import { fn } from 'storybook/test';

import { en } from '../shared/i18n/en';
import { SettingRow } from './SettingRow';

const meta = {
  title: 'Settings/SettingRow',
  component: SettingRow,
  args: {
    title: 'Theme',
    value: 'Light',
    open: false,
    onToggle: fn(),
    children: null,
  },
  render: (args) => (
    <SettingRow {...args}>
      <Text className="font-inter-medium text-body text-ink">{en.appearance.theme.note}</Text>
    </SettingRow>
  ),
} satisfies Meta<typeof SettingRow>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Closed: Story = {};
export const Open: Story = { args: { open: true } };
export const LongValue: Story = {
  args: { title: 'Text size', value: 'Largest text size for reading in dim light' },
};
