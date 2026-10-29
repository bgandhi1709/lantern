import type { ScreenName } from './events';

export const ROUTE_SCREENS: Partial<Record<string, ScreenName>> = {
  settings: 'appearance',
  'add-child': 'add-child',
  unlock: 'unlock',
  'start-again': 'start-again',
  'coming-next': 'coming-next',
  'subject/[subjectId]': 'chapters',
  'chapter/[chapterId]/history': 'chapter-history',
};
