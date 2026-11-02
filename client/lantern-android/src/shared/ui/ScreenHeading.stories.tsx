import type { Meta, StoryObj } from '@storybook/react-native';

import { en } from '../i18n/en';
import { ScreenHeading } from './ScreenHeading';

const LONG_INTRO = `${en.registration.about.intro} ${en.registration.about.intro} ${en.registration.about.intro}`;

const meta = {
  title: 'Shared/ScreenHeading',
  component: ScreenHeading,
  args: { heading: en.registration.about.heading, intro: en.registration.about.intro },
} satisfies Meta<typeof ScreenHeading>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
export const BelowAStepHeader: Story = { args: { className: 'mt-5' } };
export const LongIntro: Story = { args: { intro: LONG_INTRO } };
