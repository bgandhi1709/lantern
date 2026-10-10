import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { AboutYouView } from './AboutYouView';

const meta = {
  title: 'Registration/AboutYouView',
  component: AboutYouView,
  args: {
    parent: { name: 'Asha Verma', email: 'asha.verma@gmail.com' },
    region: '',
    noticeAccepted: false,
    canContinue: false,
    onRegionChange: fn(),
    onNoticeToggle: fn(),
    onOpenNotice: fn(),
    onContinue: fn(),
    onBack: fn(),
  },
} satisfies Meta<typeof AboutYouView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Empty: Story = {};
export const RegionOnly: Story = { args: { region: 'Pune, Maharashtra' } };
export const AgreedOnly: Story = { args: { noticeAccepted: true } };
export const Ready: Story = {
  args: { region: 'Pune, Maharashtra', noticeAccepted: true, canContinue: true },
};
