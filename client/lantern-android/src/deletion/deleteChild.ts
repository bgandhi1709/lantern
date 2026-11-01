import { z } from 'zod';

import { api } from '../shared/api';
import { notifyFamilyChanged } from '../shared/lifecycle/familyChanged';
import { writeValue } from '../shared/storage';

type Removal = { childId: string; proof: string; otherChildIds: string[] };

const removeWithProof = (childId: string, proof: string) =>
  api.delete(`/v1/family/children/${childId}`, z.unknown(), { 'Lantern-Code-Proof': proof });

export async function deleteChild({ childId, proof, otherChildIds }: Removal) {
  await removeWithProof(childId, proof);
  const next = otherChildIds[0];
  if (next) await writeValue('activeChildId', next).catch(() => undefined);
  notifyFamilyChanged();
}
