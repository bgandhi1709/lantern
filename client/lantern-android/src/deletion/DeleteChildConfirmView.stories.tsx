import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { LookProvider } from '../shared/theme';
import { LONG_NAME, TWO_CHILDREN } from '../test/homeFixtures';
import { DeleteChildConfirmView } from './DeleteChildConfirmView';

const meta = {
  title: 'Deletion/DeleteChildConfirmView',
  component: DeleteChildConfirmView,
  args: {
    family: { status: 'ready', children: TWO_CHILDREN },
    pickedId: null,
    pickedName: null,
    typedName: '',
    canSend: false,
    sending: false,
    failure: null,
    email: 'asha.verma@gmail.com',
    onPick: fn(),
    onTypeName: fn(),
    onSend: fn(),
    onCancel: fn(),
    onBack: fn(),
    onDeleteFamily: fn(),
  },
} satisfies Meta<typeof DeleteChildConfirmView>;

export default meta;

type Story = StoryObj<typeof meta>;

const picked = { pickedId: 'aarya', pickedName: 'Aarya' } as const;

export const Loading: Story = { args: { family: { status: 'loading' } } };
export const LoadFailed: Story = { args: { family: { status: 'failed' } } };
export const OnlyChild: Story = {
  args: { family: { status: 'ready', children: TWO_CHILDREN.slice(0, 1) } },
};
export const NothingPicked: Story = {};
export const Picked: Story = { args: picked };
export const NameTyped: Story = { args: { ...picked, typedName: 'Aarya', canSend: true } };
export const Sending: Story = {
  args: { ...picked, typedName: 'Aarya', canSend: true, sending: true },
};
export const EmailNotSent: Story = {
  args: { ...picked, typedName: 'Aarya', canSend: true, failure: 'emailNotSent' },
};
export const LongName_: Story = {
  args: {
    family: {
      status: 'ready',
      children: [{ id: 'long', name: LONG_NAME, classLevel: 6 }, ...TWO_CHILDREN],
    },
    pickedId: 'long',
    pickedName: LONG_NAME,
    typedName: LONG_NAME,
    canSend: true,
  },
};
export const Dark: Story = {
  args: { ...picked, typedName: 'Aarya', canSend: true },
  decorators: [
    (Story) => (
      <LookProvider appearance={{ theme: 'dark', textSize: 'default', colourScheme: 'teal' }}>
        <Story />
      </LookProvider>
    ),
  ],
};
export const LargestText: Story = {
  args: { ...picked, typedName: 'Aarya', canSend: true },
  decorators: [
    (Story) => (
      <LookProvider appearance={{ theme: 'light', textSize: 'largest', colourScheme: 'teal' }}>
        <Story />
      </LookProvider>
    ),
  ],
};
