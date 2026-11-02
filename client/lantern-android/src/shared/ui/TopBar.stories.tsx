import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { en } from '../i18n/en';
import { TopBar } from './TopBar';

const LONG_TITLE = `${en.settings.title} ${en.settings.title} ${en.settings.title} ${en.settings.title}`;

const meta = {
  title: 'Shared/TopBar',
  component: TopBar,
  args: { title: en.settings.title, onBack: fn() },
} satisfies Meta<typeof TopBar>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
export const LongTitle: Story = { args: { title: LONG_TITLE } };
