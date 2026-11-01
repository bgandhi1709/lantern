import { notifyFamilyChanged } from '../shared/lifecycle/familyChanged';
import { writeValue } from '../shared/storage';
import { deleteChildWithProof } from './oneTimeCodeApi';

type Removal = { childId: string; proof: string; otherChildIds: string[] };

export async function deleteChild({ childId, proof, otherChildIds }: Removal) {
  await deleteChildWithProof(childId, proof);
  const next = otherChildIds[0];
  if (next) await writeValue('activeChildId', next).catch(() => undefined);
  notifyFamilyChanged();
}
