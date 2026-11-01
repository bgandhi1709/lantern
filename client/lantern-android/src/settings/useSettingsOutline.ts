import { useState } from 'react';

import { useStrings } from '../shared/i18n';
import { matchesQuery } from './settingsSearch';
import type { GroupId, SettingId } from './settingsSearch';

export type Outline = {
  query: string;
  searching: boolean;
  matches: (id: SettingId) => boolean;
  isGroupOpen: (group: GroupId) => boolean;
  isRowOpen: (id: SettingId) => boolean;
  setQuery: (query: string) => void;
  clearQuery: () => void;
  toggleGroup: (group: GroupId) => void;
  toggleRow: (id: SettingId) => void;
};

export function useSettingsOutline(): Outline {
  const keywords = useStrings().settings.keywords;
  const [query, setQueryText] = useState('');
  const [groups, setGroups] = useState<Record<GroupId, boolean>>({ app: true, family: true });
  const [rows, setRows] = useState<Partial<Record<SettingId, boolean>>>({});
  const searching = query.trim() !== '';
  const isRowOpen = (id: SettingId) => rows[id] ?? searching;

  const setQuery = (text: string) => {
    setQueryText(text);
    setRows({});
  };

  return {
    query,
    searching,
    matches: (id) => matchesQuery(query, keywords[id]),
    isGroupOpen: (group) => searching || groups[group],
    isRowOpen,
    setQuery,
    clearQuery: () => setQuery(''),
    toggleGroup: (group) => {
      if (!searching) setGroups({ ...groups, [group]: !groups[group] });
    },
    toggleRow: (id) => setRows({ ...rows, [id]: !isRowOpen(id) }),
  };
}
