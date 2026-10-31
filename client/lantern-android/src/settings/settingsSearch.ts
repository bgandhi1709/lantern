export const APP_IDS = ['theme', 'textSize', 'colour', 'preview'] as const;
export const FAMILY_IDS = ['unlock', 'deleteChild', 'deleteFamily', 'startAgain'] as const;

export type SettingId = (typeof APP_IDS)[number] | (typeof FAMILY_IDS)[number];
export type GroupId = 'app' | 'family';

export function matchesQuery(query: string, keywords: string) {
  return query
    .toLowerCase()
    .split(/\s+/)
    .filter(Boolean)
    .every((word) => keywords.includes(word));
}
