import { unlockText } from '../shared/crypto';
import { fetchFamily } from './homeApi';
import type { HomeChild } from './useHome';

export async function fetchUnlockedFamily(familyKey: string) {
  const { board, children } = await fetchFamily();
  return {
    board,
    children: children.map((row): HomeChild => ({
      id: row.childId,
      name: unlockText(familyKey, row.name),
      classLevel: row.classLevel,
    })),
  };
}
