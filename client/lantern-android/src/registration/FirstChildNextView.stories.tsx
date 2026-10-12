import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { FirstChildNextView } from './FirstChildNextView';

const meta = {
  title: 'Registration/FirstChildNextView',
  component: FirstChildNextView,
  args: { onBack: fn() },
} satisfies Meta<typeof FirstChildNextView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
