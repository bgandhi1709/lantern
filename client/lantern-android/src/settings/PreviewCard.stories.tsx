import type { Meta, StoryObj } from '@storybook/react-native';

import { PreviewCard } from './PreviewCard';

const meta = { title: 'Settings/PreviewCard', component: PreviewCard } satisfies Meta<
  typeof PreviewCard
>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
