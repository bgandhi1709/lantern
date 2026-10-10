import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { en } from '../i18n/en';
import { PinIcon } from './icons';
import { TextField } from './TextField';

const about = en.registration.about;

const meta = {
  title: 'Shared/TextField',
  component: TextField,
  args: {
    label: about.regionLabel,
    hint: about.regionHint,
    placeholder: about.regionPlaceholder,
    value: '',
    onChangeText: fn(),
    icon: <PinIcon />,
    maxLength: 60,
  },
} satisfies Meta<typeof TextField>;

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
