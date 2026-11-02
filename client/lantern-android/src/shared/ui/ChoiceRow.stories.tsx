import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { en } from '../i18n/en';
import { ChoiceRow } from './ChoiceRow';
import { CheckCircleIcon, UserIcon } from './icons';

const NAME = 'Aarya';
const CLASS = en.home.classLabel(6);
const LONG_TITLE = 'ઉમાશંકર જોશી અને તેમના મિત્રોનું લાંબું નામ';

function Row(props: Partial<React.ComponentProps<typeof ChoiceRow>>) {
  return (
    <ChoiceRow
      role="button"
      selected={false}
      label={`${NAME}, ${CLASS}`}
      onPress={fn()}
      leading={<UserIcon />}
      title={NAME}
      subtitle={CLASS}
      trailing={<CheckCircleIcon />}
      {...props}
    />
  );
}

const meta = { title: 'Shared/ChoiceRow', component: Row } satisfies Meta<typeof Row>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Unselected: Story = {};
export const Selected: Story = { args: { selected: true } };
export const Radio: Story = { args: { role: 'radio', selected: true } };
export const LongTitle: Story = { args: { title: LONG_TITLE, selected: true } };
