import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { LookProvider } from '../shared/theme';
import { DeleteCodeView } from './DeleteCodeView';

const meta = {
  title: 'Deletion/DeleteCodeView',
  component: DeleteCodeView,
  args: {
    intro:
      'We emailed a 6-digit code to asha.verma@gmail.com. Type it to delete the child you chose.',
    deleteLabel: 'Delete child',
    cancelLabel: 'Cancel',
    code: '',
    verified: false,
    work: null,
    failure: null,
    resent: false,
    onTypeCode: fn(),
    onResend: fn(),
    onDelete: fn(),
    onCancel: fn(),
    onBack: fn(),
  },
} satisfies Meta<typeof DeleteCodeView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Empty: Story = {};
export const Typing: Story = { args: { code: '4829' } };
export const Checking: Story = { args: { code: '482917', work: 'verifying' } };
export const Verified: Story = { args: { code: '482917', verified: true } };
export const WrongCode: Story = { args: { code: '111111', failure: 'wrongCode' } };
export const ExpiredCode: Story = { args: { code: '482917', failure: 'codeExpired' } };
export const TooSoon: Story = { args: { failure: 'tooSoon', resent: false } };
export const Resent: Story = { args: { resent: true } };
export const Deleting: Story = {
  args: { code: '482917', verified: true, work: 'acting' },
};
export const DeleteFailed: Story = {
  args: { code: '482917', verified: true, failure: 'other' },
};
export const Dark: Story = {
  args: { code: '482917', verified: true },
  decorators: [
    (Story) => (
      <LookProvider appearance={{ theme: 'dark', textSize: 'default', colourScheme: 'teal' }}>
        <Story />
      </LookProvider>
    ),
  ],
};
export const LargestText: Story = {
  args: { code: '482917', verified: true },
  decorators: [
    (Story) => (
      <LookProvider appearance={{ theme: 'light', textSize: 'largest', colourScheme: 'teal' }}>
        <Story />
      </LookProvider>
    ),
  ],
};
export const ForTheFamily: Story = {
  args: {
    intro: 'We emailed a 6-digit code to asha.verma@gmail.com. Type it to delete your Family.',
    deleteLabel: 'Delete my Family forever',
    cancelLabel: 'Keep my Family',
    lastStep: 'This is the last step. There is no undo.',
    code: '482917',
    verified: true,
  },
};
