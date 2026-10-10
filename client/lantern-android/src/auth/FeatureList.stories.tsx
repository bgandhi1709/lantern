import type { Meta, StoryObj } from '@storybook/react-native';

import { FeatureList } from './FeatureList';

const meta = { title: 'Auth/FeatureList', component: FeatureList } satisfies Meta<
  typeof FeatureList
>;

export default meta;

export const Default: StoryObj<typeof meta> = {};
