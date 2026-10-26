import { unlockText } from '../shared/crypto';
import { fetchChildren } from './homeApi';
import type { HomeChild } from './useHome';

export async function fetchUnlockedChildren(familyKey: string): Promise<HomeChild[]> {
  const rows = await fetchChildren();
  return rows.map((row) => ({
    id: row.childId,
    name: unlockText(familyKey, row.name),
    classLevel: row.classLevel,
  }));
}
