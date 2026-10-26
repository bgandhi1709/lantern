import { lockBytes, makeFamilyKey, unlockBytes } from './lock';
import { deriveKey, fromBase64, randomBytes, randomUUID, toBase64 } from './native';
import type { KeyCost } from './native';
import { formatRecoveryCode, makeRecoveryCode } from './recoveryCode';

// The cost of "v1" wrapped keys (see lock.ts): 64 MiB and 3 passes take about a second on a
// phone, and make a stolen wrapped key slow to guess. Changing it means a new version.
const WRAP_COST: KeyCost = { memoryKiB: 65_536, passes: 3 };
const SALT_BYTES = 16;

export type FamilyKeys = {
  familyKey: string;
  recoveryWrappedKey: string;
  recoverySalt: string;
  recoveryCode: string;
};

export const makeFamilyId = () => randomUUID();

export const makeChildId = () => randomUUID();

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

export const rewrapFamilyKeys = (familyId: string, keys: FamilyKeys) =>
  wrapWithCode(familyId, keys.familyKey, keys.recoveryCode);

export async function openWithRecoveryCode(
  familyId: string,
  typedCode: string,
  wrappedKey: string,
  salt: string,
) {
  const key = await wrappingKey(familyId, formatRecoveryCode(typedCode), fromBase64(salt));
  return toBase64(unlockBytes(key, wrappedKey));
}
