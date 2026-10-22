import { lockBytes, makeFamilyKey, unlockBytes } from './lock';
import { deriveKey, fromBase64, randomBytes, randomUUID, toBase64 } from './native';
import type { KeyCost } from './native';
import { formatRecoveryCode, makeRecoveryCode } from './recoveryCode';

// The cost of "v1" wrapped keys (see lock.ts): 64 MiB and 3 passes take about a second on a
// phone, and make a stolen wrapped key slow to guess. Changing it means a new version.
const WRAP_COST: KeyCost = { memoryKiB: 65_536, passes: 3 };
const SALT_BYTES = 16;

export type FamilyKeys = {
  // Kept on the phone (and only after registering). The other fields go to /v1/register.
  familyKey: string;
  recoveryWrappedKey: string;
  recoverySalt: string;
  // Shown to the Parent once and emailed by them to themself. Never stored and never sent.
  recoveryCode: string;
};

export const makeFamilyId = () => randomUUID();

// The Family id is part of the input, so two Families with the same Recovery code get different keys.
// NFC makes an accented letter the same bytes whichever keyboard typed it.
const wrappingKey = (familyId: string, secret: string, salt: Uint8Array) =>
  deriveKey(`${familyId}_${secret.normalize('NFC')}`, salt, WRAP_COST);

async function wrap(familyKey: string, familyId: string, secret: string) {
  const salt = randomBytes(SALT_BYTES);
  const key = await wrappingKey(familyId, secret, salt);
  return { wrappedKey: lockBytes(key, fromBase64(familyKey)), salt: toBase64(salt) };
}

async function wrapWithCode(
  familyId: string,
  familyKey: string,
  recoveryCode: string,
): Promise<FamilyKeys> {
  const { wrappedKey, salt } = await wrap(familyKey, familyId, recoveryCode);
  return { familyKey, recoveryWrappedKey: wrappedKey, recoverySalt: salt, recoveryCode };
}

export const createFamilyKeys = (familyId: string) =>
  wrapWithCode(familyId, makeFamilyKey(), makeRecoveryCode());

// The Family id is part of what locks the wrapped key, so a new id needs a new copy. The Family key
// and the Recovery code stay as they were, so the code the Parent emailed still opens the Family.
export const rewrapFamilyKeys = (familyId: string, keys: FamilyKeys) =>
  wrapWithCode(familyId, keys.familyKey, keys.recoveryCode);

// Throws LockError when the code is wrong; the code is never part of the message.
export async function openWithRecoveryCode(
  familyId: string,
  typedCode: string,
  wrappedKey: string,
  salt: string,
) {
  const key = await wrappingKey(familyId, formatRecoveryCode(typedCode), fromBase64(salt));
  return toBase64(unlockBytes(key, wrappedKey));
}
