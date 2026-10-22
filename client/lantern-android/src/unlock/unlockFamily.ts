import { z } from 'zod';

import { api } from '../shared/api';
import { LockError, openWithRecoveryCode } from '../shared/crypto';
import { keepFamilyKey, readValue, writeValue } from '../shared/storage';
import { waitSeconds } from './unlockWait';

const FamilyLockSchema = z.object({
  familyId: z.string(),
  recoveryWrappedKey: z.string(),
  recoverySalt: z.string(),
});

export type FamilyLock = z.infer<typeof FamilyLockSchema>;
type Attempt = { uid: string; typed: string; now?: number };

export class UnlockWaitError extends Error {
  constructor(readonly secondsLeft: number) {
    super('Wait before trying again.');
    this.name = 'UnlockWaitError';
  }
}

export const fetchFamilyLock = () => api.get('/v1/me', FamilyLockSchema);

const MS_PER_SECOND = 1000;

async function refuseDuringWait(now: number) {
  const { until } = await readValue('unlockWait');
  if (until > now) throw new UnlockWaitError(Math.ceil((until - now) / MS_PER_SECOND));
}

async function recordWrongTry(now: number) {
  const { failures } = await readValue('unlockWait');
  const next = failures + 1;
  await writeValue('unlockWait', {
    failures: next,
    until: now + waitSeconds(next) * MS_PER_SECOND,
  });
}

const open = (lock: FamilyLock, { typed }: Attempt) =>
  openWithRecoveryCode(lock.familyId, typed, lock.recoveryWrappedKey, lock.recoverySalt);

export async function unlockFamily(lock: FamilyLock, attempt: Attempt) {
  const now = attempt.now ?? Date.now();
  await refuseDuringWait(now);
  try {
    await keepFamilyKey(attempt.uid, await open(lock, attempt));
  } catch (error) {
    if (error instanceof LockError) await recordWrongTry(now);
    throw error;
  }
  await writeValue('unlockWait', { failures: 0, until: 0 });
}
