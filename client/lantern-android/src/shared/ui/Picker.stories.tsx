import type { Meta, StoryObj } from '@storybook/react-native';
import type { ComponentProps } from 'react';
import { fn } from 'storybook/test';

import { en } from '../i18n/en';
import { CalendarIcon } from './icons';
import { Picker } from './Picker';

const child = en.registration.firstChild;

// The icon is a node, and Storybook's args must stay plain data, so the story fixes it.
function YearPicker({
  bare = false,
  ...props
}: Omit<ComponentProps<typeof Picker>, 'icon'> & { bare?: boolean }) {
  return <Picker {...props} icon={bare ? undefined : <CalendarIcon />} />;
}

const meta = {
  title: 'Shared/Picker',
  component: YearPicker,
  args: {
    label: child.birthYearLabel,
    placeholder: child.birthYearPlaceholder,
    value: '',
    options: ['2023', '2022', '2021', '2020'].map((year) => ({ value: year, label: year })),
    onChange: fn(),
  },
} satisfies Meta<typeof YearPicker>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Empty: Story = {};
export const Selected: Story = { args: { value: '2021' } };
export const WithHint: Story = { args: { hint: child.sscHint } };
export const WithoutIcon: Story = { args: { bare: true } };
// Gujarati and Hindi labels run longer than English, so the label must wrap.
export const LongLabel: Story = {
  args: { label: 'બાળકનું જન્મ વર્ષ, જે તમારા બાળકના શાળાના દાખલામાં લખેલું છે', value: '2022' },
};
