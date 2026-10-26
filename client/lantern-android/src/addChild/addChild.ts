import { z } from 'zod';

import { api } from '../shared/api';
import { lockedChild } from '../registration';
import type { ChildDraft } from '../registration';
import { getParent } from '../shared/session';
import { readFamilyKey, writeValue } from '../shared/storage';

const Added = z.object({ childId: z.string() });

export type NewChild = { childId: string; draft: ChildDraft };

export async function addChild({ childId, draft }: NewChild) {
  const familyKey = await readFamilyKey(getParent().uid);
  if (!familyKey) throw new Error('Adding a Child needs the Family key on this phone.');
  await api.post('/v1/family/children', Added, { childId, ...lockedChild(familyKey, draft) });
  await writeValue('activeChildId', childId).catch(() => undefined);
}
