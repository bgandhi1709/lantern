import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { AboutYouView } from './AboutYouView';

const meta = {
  title: 'Registration/AboutYouView',
  component: AboutYouView,
  args: { parent: { name: 'Asha Verma', email: 'asha.verma@gmail.com' }, onBack: fn() },
} satisfies Meta<typeof AboutYouView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Empty: Story = {};
