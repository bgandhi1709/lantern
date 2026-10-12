import type { Meta, StoryObj } from '@storybook/react-native';

import { KeepSafeNote } from './KeepSafeNote';

const meta = { title: 'Registration/KeepSafeNote', component: KeepSafeNote } satisfies Meta<
  typeof KeepSafeNote
>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
