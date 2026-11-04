import { z } from 'zod';

import { api } from '../shared/api';
import { clearAll } from '../shared/storage';

export async function deleteFamily(proof: string) {
  await api.delete('/v1/family', z.unknown(), { 'Lantern-Code-Proof': proof });
  await clearAll();
}
