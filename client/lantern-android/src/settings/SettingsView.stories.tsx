import type { Meta, StoryObj } from '@storybook/react-native';
import { fn } from 'storybook/test';

import { en } from '../shared/i18n/en';
import { LookProvider } from '../shared/theme';
import type { Look } from './AppSettings';
import { SettingsView } from './SettingsView';
import { matchesQuery } from './settingsSearch';
import type { GroupId, SettingId } from './settingsSearch';
import type { Outline } from './useSettingsOutline';

type OutlineState = { query?: string; open?: SettingId[]; closedGroups?: GroupId[] };

function outlineOf({ query = '', open, closedGroups = [] }: OutlineState = {}): Outline {
  const searching = query.trim() !== '';
  return {
    query,
    searching,
    matches: (id) => matchesQuery(query, en.settings.keywords[id]),
    isGroupOpen: (group) => searching || !closedGroups.includes(group),
    isRowOpen: (id) => (open ? open.includes(id) : searching),
    setQuery: fn(),
    clearQuery: fn(),
    toggleGroup: fn(),
    toggleRow: fn(),
  };
}

const LOOK: Look = {
  theme: 'light',
  textSize: 'default',
  colourScheme: 'teal',
  onTheme: fn(),
  onTextSize: fn(),
  onColourScheme: fn(),
};

const meta = {
  title: 'Settings/SettingsView',
  component: SettingsView,
  args: {
    look: LOOK,
    family: { deleteChild: fn(), deleteFamily: fn() },
    outline: outlineOf(),
    onBack: fn(),
  },
  decorators: [
    (Story, { args }) => (
      <LookProvider
        appearance={{
          theme: args.look.theme,
          textSize: args.look.textSize,
          colourScheme: args.look.colourScheme,
        }}
      >
        <Story />
      </LookProvider>
    ),
  ],
} satisfies Meta<typeof SettingsView>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};
export const DarkTeal: Story = { args: { look: { ...LOOK, theme: 'dark' } } };
export const BlueLargeText: Story = {
  args: { look: { ...LOOK, colourScheme: 'blue', textSize: 'large' } },
};
export const GrayDark: Story = { args: { look: { ...LOOK, colourScheme: 'gray', theme: 'dark' } } };
export const GoldenLargestText: Story = {
  args: { look: { ...LOOK, colourScheme: 'golden', textSize: 'largest' } },
};

export const FamilyLocked: Story = {
  args: { family: { unlock: fn(), startAgain: fn() } },
};

export const ThemeOpen: Story = { args: { outline: outlineOf({ open: ['theme'] }) } };
export const ColourOpen: Story = { args: { outline: outlineOf({ open: ['colour'] }) } };
export const PreviewOpen: Story = { args: { outline: outlineOf({ open: ['preview'] }) } };
export const AppSettingsCollapsed: Story = {
  args: { outline: outlineOf({ closedGroups: ['app'] }) },
};
export const FamilyManagementCollapsed: Story = {
  args: { outline: outlineOf({ closedGroups: ['family'] }) },
};
export const SearchDark: Story = { args: { outline: outlineOf({ query: 'dark' }) } };
export const SearchDelete: Story = { args: { outline: outlineOf({ query: 'erase' }) } };
export const NothingFound: Story = { args: { outline: outlineOf({ query: 'zzz' }) } };
