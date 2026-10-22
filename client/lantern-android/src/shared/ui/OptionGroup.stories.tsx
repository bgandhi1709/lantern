import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { OptionGroup } from './OptionGroup';

const meta = {
  title: 'Shared/OptionGroup',
  component: OptionGroup,
  args: { onPick: fn() },
} satisfies Meta<typeof OptionGroup>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Theme: Story = {
  args: {
    label: 'Theme',
    value: 'light',
    options: [
      { value: 'light', label: 'Light', accessibilityLabel: 'Light' },
      { value: 'dark', label: 'Dark', accessibilityLabel: 'Dark' },
    ],
  },
};

export const TextSize: Story = {
  args: {
    label: 'Text size',
    value: 'large',
    options: [
      {
        value: 'default',
        label: 'Aa',
        accessibilityLabel: 'Normal',
        textClassName: 'text-aa-default',
      },
      { value: 'large', label: 'Aa', accessibilityLabel: 'Large', textClassName: 'text-aa-large' },
      {
        value: 'largest',
        label: 'Aa',
        accessibilityLabel: 'Largest',
        textClassName: 'text-aa-largest',
      },
    ],
  },
};

export const LongLabels: Story = {
  args: {
    label: 'Theme',
    value: 'dark',
    options: [
      { value: 'light', label: 'પ્રકાશિત', accessibilityLabel: 'પ્રકાશિત' },
      { value: 'dark', label: 'ઘેરો રંગ', accessibilityLabel: 'ઘેરો રંગ' },
    ],
  },
};
