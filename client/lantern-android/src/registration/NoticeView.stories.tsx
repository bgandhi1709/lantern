import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { NoticeView } from './NoticeView';

const meta = {
  title: 'Registration/NoticeView',
  component: NoticeView,
  args: { version: '2026-10', onBack: fn() },
} satisfies Meta<typeof NoticeView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
