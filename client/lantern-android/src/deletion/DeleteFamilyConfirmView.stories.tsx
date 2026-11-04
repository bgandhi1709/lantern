import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { LookProvider } from '../shared/theme';
import { LONG_NAME, TWO_CHILDREN } from '../test/homeFixtures';
import { DeleteFamilyConfirmView } from './DeleteFamilyConfirmView';

const meta = {
  title: 'Deletion/DeleteFamilyConfirmView',
  component: DeleteFamilyConfirmView,
  args: {
    family: { status: 'ready', children: TWO_CHILDREN },
    typed: '',
    canSend: false,
    sending: false,
    failure: null,
    email: 'asha.verma@gmail.com',
    onType: fn(),
    onSend: fn(),
    onCancel: fn(),
    onBack: fn(),
  },
} satisfies Meta<typeof DeleteFamilyConfirmView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
export const Loading: Story = { args: { family: { status: 'loading' } } };
export const WithoutNames: Story = { args: { family: { status: 'failed' } } };
export const Typing: Story = { args: { typed: 'DELET' } };
export const Typed: Story = { args: { typed: 'DELETE', canSend: true } };
export const Sending: Story = { args: { typed: 'DELETE', canSend: true, sending: true } };
export const EmailNotSent: Story = {
  args: { typed: 'DELETE', canSend: true, failure: 'emailNotSent' },
};
export const LongNames: Story = {
  args: {
    family: {
      status: 'ready',
      children: [...TWO_CHILDREN, { ...TWO_CHILDREN[0], id: 'long', name: LONG_NAME }],
    },
  },
};
export const Dark: Story = {
  args: { typed: 'DELETE', canSend: true },
  decorators: [
    (Story) => (
      <LookProvider appearance={{ theme: 'dark', textSize: 'default', colourScheme: 'teal' }}>
        <Story />
      </LookProvider>
    ),
  ],
};
export const LargestText: Story = {
  args: { typed: 'DELETE', canSend: true },
  decorators: [
    (Story) => (
      <LookProvider appearance={{ theme: 'light', textSize: 'largest', colourScheme: 'teal' }}>
        <Story />
      </LookProvider>
    ),
  ],
};
