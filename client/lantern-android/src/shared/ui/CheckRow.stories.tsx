import type { Meta, StoryObj } from '@storybook/react-native';
import { Text } from 'react-native';
import { fn } from 'storybook/test';

import { en } from '../i18n/en';
import { CheckRow } from './CheckRow';
import { ShieldCheckIcon } from './icons';

const about = en.registration.about;

const LONG_LABEL =
  'મેં ગોપનીયતા સૂચના વાંચી છે અને હું તેની સાથે સંમત છું, અને મારા બાળકની માહિતી કેવી રીતે વપરાય છે તે મને સમજાયું છે.';

// The icon and label are nodes, and Storybook's args must stay plain data, so the story fixes them.
function AgreementRow({
  checked,
  onToggle,
  long = false,
  bare = false,
}: {
  checked: boolean;
  onToggle: () => void;
  long?: boolean;
  bare?: boolean;
}) {
  return (
    <CheckRow
      checked={checked}
      onToggle={onToggle}
      accessibilityLabel={about.agreeLabel}
      icon={bare ? undefined : <ShieldCheckIcon />}
    >
      <Text className="font-inter-medium text-body text-ink">
        {long ? (
          LONG_LABEL
        ) : (
          <>
            {about.agreeBefore}
            <Text className="font-inter-bold text-accent underline">{about.noticeLink}</Text>
            {about.agreeAfter}
          </>
        )}
      </Text>
    </CheckRow>
  );
}

const meta = {
  title: 'Shared/CheckRow',
  component: AgreementRow,
  args: { checked: false, onToggle: fn() },
} satisfies Meta<typeof AgreementRow>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Unchecked: Story = {};
export const Checked: Story = { args: { checked: true } };
// Gujarati and Hindi run longer than English, so the row must grow instead of squeezing the box.
export const LongLabel: Story = { args: { checked: true, long: true } };
export const WithoutIcon: Story = { args: { bare: true } };
