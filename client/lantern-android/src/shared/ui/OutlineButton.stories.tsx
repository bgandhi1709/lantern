import type { Meta, StoryObj } from '@storybook/react-native';
import type { ComponentProps } from 'react';
import { fn } from 'storybook/test';

import { en } from '../i18n/en';
import { MailIcon } from './icons';
import { OutlineButton } from './OutlineButton';

// The icon is a node, and Storybook's args must stay plain data, so the story fixes it.
function MailButton({
  withIcon = true,
  ...props
}: Omit<ComponentProps<typeof OutlineButton>, 'icon'> & { withIcon?: boolean }) {
  return <OutlineButton {...props} icon={withIcon ? <MailIcon /> : undefined} />;
}

const meta = {
  title: 'Shared/OutlineButton',
  component: MailButton,
  args: { label: en.registration.recovery.email, onPress: fn() },
} satisfies Meta<typeof MailButton>;

export default meta;

type Story = StoryObj<typeof meta>;

export const WithIcon: Story = {};
export const WithoutIcon: Story = { args: { withIcon: false } };
// Gujarati and Hindi labels run longer than English, so the label must wrap beside the icon.
export const LongLabel: Story = {
  args: { label: 'મારા પોતાના ઇમેઇલ પર મારો પુનઃપ્રાપ્તિ કોડ મોકલો' },
};
