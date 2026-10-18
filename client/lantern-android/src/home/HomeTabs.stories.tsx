import type { Meta, StoryObj } from '@storybook/react-native';

import { HomeTabs } from './HomeTabs';

const meta = { title: 'Home/HomeTabs', component: HomeTabs } satisfies Meta<typeof HomeTabs>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
