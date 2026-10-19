import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { LookProvider } from '../shared/theme';
import { AppearanceView } from './AppearanceView';

const meta = {
  title: 'Settings/AppearanceView',
  component: AppearanceView,
  args: {
    theme: 'light',
    textSize: 'default',
    colourScheme: 'teal',
    onTheme: fn(),
    onTextSize: fn(),
    onColourScheme: fn(),
    onBack: fn(),
  },
  decorators: [
    (Story, { args }) => (
      <LookProvider
        appearance={{ theme: args.theme, textSize: args.textSize, colourScheme: args.colourScheme }}
      >
        <Story />
      </LookProvider>
    ),
  ],
} satisfies Meta<typeof AppearanceView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
export const DarkTeal: Story = { args: { theme: 'dark' } };
export const BlueLargeText: Story = { args: { colourScheme: 'blue', textSize: 'large' } };
export const GrayDark: Story = { args: { colourScheme: 'gray', theme: 'dark' } };
export const GoldenLargestText: Story = { args: { colourScheme: 'golden', textSize: 'largest' } };
