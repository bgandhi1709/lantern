import type { Meta, StoryObj } from '@storybook/react-native';
import { ChapterStateTracker } from './ChapterStateTracker';

const meta = {
  title: 'Home/ChapterStateTracker',
  component: ChapterStateTracker,
  args: { state: 'planned' },
} satisfies Meta<typeof ChapterStateTracker>;

export default meta;

type Story = StoryObj<typeof meta>;

export const NotStarted: Story = { args: { state: 'not-started' } };
export const Planned: Story = {};
export const Taught: Story = { args: { state: 'taught' } };
export const Checked: Story = { args: { state: 'checked' } };
