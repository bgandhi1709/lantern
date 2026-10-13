import type { Meta, StoryObj } from '@storybook/react-native';
import type { ComponentProps } from 'react';
import { fn } from 'storybook/test';

import { birthYearChoices } from './child';
import type { ChildDraft } from './child';
import { ChildCard } from './ChildCard';
import { ChildFields } from './ChildFields';

// The fields are a node, and Storybook's args must stay plain data, so the story fixes them.
function CardWithFields({
  child,
  ...props
}: Omit<ComponentProps<typeof ChildCard>, 'children'> & { child: ChildDraft }) {
  return (
    <ChildCard {...props}>
      <ChildFields
        idPrefix="story"
        child={child}
        board="cbse"
        years={birthYearChoices(new Date('2026-10-13'))}
        classAsList
        onChange={fn()}
      />
    </ChildCard>
  );
}

const ready = { name: 'Mira', classLevel: 2, birthYear: '2020', school: '' };

const meta = {
  title: 'Registration/ChildCard',
  component: CardWithFields,
  args: {
    number: 2,
    name: '',
    classLevel: null,
    status: 'optional',
    open: false,
    child: { name: '', classLevel: null, birthYear: '', school: '' },
    onToggle: fn(),
    onRemove: fn(),
  },
} satisfies Meta<typeof CardWithFields>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Optional: Story = {};
export const NeedsMore: Story = {
  args: { name: 'Mira', status: 'needsMore', child: { ...ready, classLevel: null } },
};
export const Ready: Story = {
  args: { name: 'Mira', classLevel: 2, status: 'ready', child: ready },
};
export const Open: Story = {
  args: { name: 'Mira', classLevel: 2, status: 'ready', open: true, child: ready },
};
export const OpenAndEmpty: Story = { args: { open: true } };
export const LongName: Story = {
  args: {
    name: 'Venkatasubramanian Padmanabhan Iyer',
    classLevel: 2,
    status: 'ready',
    child: { ...ready, name: 'Venkatasubramanian Padmanabhan Iyer' },
  },
};
