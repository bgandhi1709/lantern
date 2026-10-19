import type { Meta, StoryObj } from '@storybook/react-native';
import { View } from 'react-native';

import { LoadingOverlay } from './LoadingOverlay';

const meta = {
  title: 'Shared/LoadingOverlay',
  component: LoadingOverlay,
  args: { label: 'Setting up your family…' },
  decorators: [
    (Story) => (
      <View className="flex-1">
        <Story />
      </View>
    ),
  ],
} satisfies Meta<typeof LoadingOverlay>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};

export const LongLabel: Story = {
  args: { label: 'તમારો પરિવાર તૈયાર કરી રહ્યા છીએ, થોડી રાહ જુઓ…' },
};
