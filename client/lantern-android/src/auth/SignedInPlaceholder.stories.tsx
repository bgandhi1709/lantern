import type { Meta, StoryObj } from '@storybook/react-native';

import { SignedInPlaceholder } from './SignedInPlaceholder';

const meta = { title: 'Auth/SignedInPlaceholder', component: SignedInPlaceholder } satisfies Meta<
  typeof SignedInPlaceholder
>;

export default meta;

export const Default: StoryObj<typeof meta> = {};
