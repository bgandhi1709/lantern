import { api } from '../shared/api';
import { clearAll } from '../shared/storage';
import { z } from 'zod';

export async function startAgain() {
  await api.delete('/v1/family', z.unknown());
  await clearAll();
}
