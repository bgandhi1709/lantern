import type { Meta, StoryObj } from '@storybook/react-native';

import { Mark } from './Mark';

const meta = { title: 'Shared/Mark', component: Mark } satisfies Meta<typeof Mark>;

export default meta;

export const Default: StoryObj<typeof meta> = {};
