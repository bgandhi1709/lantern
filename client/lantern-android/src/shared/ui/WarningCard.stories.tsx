import type { Meta, StoryObj } from '@storybook/react-native';

import { en } from '../i18n/en';
import { WarningCard } from './WarningCard';

const meta = {
  title: 'Shared/WarningCard',
  component: WarningCard,
  args: { heading: en.startAgain.warningHeading, text: en.startAgain.warning },
} satisfies Meta<typeof WarningCard>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
export const LongText: Story = {
  args: { text: `${en.startAgain.warning} ${en.startAgain.warning} ${en.startAgain.warning}` },
};
