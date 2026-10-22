import type { Meta, StoryObj } from '@storybook/react-native';
import type { ComponentProps } from 'react';
import { fn } from 'storybook/test';

import { en } from '../i18n/en';
import { PinIcon } from './icons';
import { TextField } from './TextField';

const about = en.registration.about;

// The icon is a node, and Storybook's args must stay plain data, so the story fixes it.
function RegionField({
  bare = false,
  ...props
}: Omit<ComponentProps<typeof TextField>, 'icon'> & { bare?: boolean }) {
  return <TextField {...props} icon={bare ? undefined : <PinIcon />} />;
}

const meta = {
  title: 'Shared/TextField',
  component: RegionField,
  args: {
    label: about.regionLabel,
    hint: about.regionHint,
    placeholder: about.regionPlaceholder,
    value: '',
    onChangeText: fn(),
    maxLength: 60,
  },
} satisfies Meta<typeof RegionField>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Empty: Story = {};
export const Filled: Story = { args: { value: 'Pune, Maharashtra' } };
// Gujarati and Hindi labels and hints run longer than English, so the field must wrap them.
export const LongLabel: Story = {
  args: {
    label: 'તમે કયા શહેર અને રાજ્યમાં રહો છો, જ્યાં તમારું બાળક ભણે છે?',
    hint: 'ઉદાહરણ તરીકે: પુણે, મહારાષ્ટ્ર અથવા અમદાવાદ, ગુજરાત',
    value: 'अहमदाबाद, गुजरात',
  },
};
// A secret is hidden as it is typed, and a field with no icon starts its text at the edge.
export const Secret: Story = {
  args: {
    bare: true,
    secret: true,
    label: en.unlock.label,
    hint: en.unlock.hint,
    placeholder: undefined,
    value: 'K7QM-4TDX-92HB-WEPN',
  },
};
export const WithError: Story = {
  args: {
    bare: true,
    secret: true,
    label: en.unlock.label,
    hint: undefined,
    placeholder: undefined,
    error: en.unlock.wrong,
    value: 'K7QM-4TDX',
  },
};
