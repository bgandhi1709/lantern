import type { Meta, StoryObj } from '@storybook/react-native';
import { Text } from 'react-native';
import { fn } from 'storybook/test';

import { en } from '../i18n/en';
import { ShieldCheckIcon } from './icons';
import { CheckRow } from './CheckRow';

const about = en.registration.about;

const meta = {
  title: 'Shared/CheckRow',
  component: CheckRow,
  args: {
    checked: false,
    onToggle: fn(),
    accessibilityLabel: about.agreeLabel,
    icon: <ShieldCheckIcon />,
    children: (
      <Text className="font-inter-medium text-body text-ink">
        {about.agreeBefore}
        <Text className="font-inter-bold text-primary underline">{about.noticeLink}</Text>
        {about.agreeAfter}
      </Text>
    ),
  },
} satisfies Meta<typeof CheckRow>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Unchecked: Story = {};
export const Checked: Story = { args: { checked: true } };
// Gujarati and Hindi run longer than English, so the row must grow instead of squeezing the box.
export const LongLabel: Story = {
  args: {
    checked: true,
    children: (
      <Text className="font-inter-medium text-body text-ink">
        મેં ગોપનીયતા સૂચના વાંચી છે અને હું તેની સાથે સંમત છું, અને મારા બાળકની માહિતી કેવી રીતે
        વપરાય છે તે મને સમજાયું છે.
      </Text>
    ),
  },
};
