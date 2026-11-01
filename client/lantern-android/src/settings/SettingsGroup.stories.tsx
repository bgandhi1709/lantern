import type { Meta, StoryObj } from '@storybook/react-native';
import { Text } from 'react-native';
import { fn } from 'storybook/test';

import { en } from '../shared/i18n/en';
import { SettingsGroup } from './SettingsGroup';

const meta = {
  title: 'Settings/SettingsGroup',
  component: SettingsGroup,
  args: {
    title: 'App settings',
    open: true,
    onToggle: fn(),
    children: null,
  },
  render: (args) => (
    <SettingsGroup {...args}>
      <Text className="type-body">{en.appearance.theme.note}</Text>
    </SettingsGroup>
  ),
} satisfies Meta<typeof SettingsGroup>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Open: Story = {};
export const Closed: Story = { args: { open: false } };
export const LongTitle: Story = {
  args: { title: 'A very long group title that still has to fit beside its arrow' },
};
