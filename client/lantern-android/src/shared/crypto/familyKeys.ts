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
  passphraseWrappedKey: string;
  passphraseSalt: string;
  recoveryWrappedKey: string;
  recoverySalt: string;
  // Shown to the Parent once. Never stored and never sent.
  recoveryCode: string;
};

export const makeFamilyId = () => randomUUID();

// The Family id is part of the input, so two Families with the same Passphrase get different keys.
// NFC makes an accented letter the same bytes whichever keyboard typed it.
const wrappingKey = (familyId: string, secret: string, salt: Uint8Array) =>
  deriveKey(`${familyId}_${secret.normalize('NFC')}`, salt, WRAP_COST);

async function wrap(familyKey: string, familyId: string, secret: string) {
  const salt = randomBytes(SALT_BYTES);
  const key = await wrappingKey(familyId, secret, salt);
  return { wrappedKey: lockBytes(key, fromBase64(familyKey)), salt: toBase64(salt) };
}

async function unwrap(familyId: string, secret: string, wrappedKey: string, salt: string) {
  const key = await wrappingKey(familyId, secret, fromBase64(salt));
  return toBase64(unlockBytes(key, wrappedKey));
}

async function wrapBoth(
  familyId: string,
  passphrase: string,
  familyKey: string,
  recoveryCode: string,
): Promise<FamilyKeys> {
  const byPassphrase = await wrap(familyKey, familyId, passphrase);
  const byCode = await wrap(familyKey, familyId, recoveryCode);
  return {
    familyKey,
    passphraseWrappedKey: byPassphrase.wrappedKey,
    passphraseSalt: byPassphrase.salt,
    recoveryWrappedKey: byCode.wrappedKey,
    recoverySalt: byCode.salt,
    recoveryCode,
  };
}

export const createFamilyKeys = (familyId: string, passphrase: string) =>
  wrapBoth(familyId, passphrase, makeFamilyKey(), makeRecoveryCode());

// The Family id is part of what locks the wrapped keys, so a new id needs new copies. The Family key
// and the Recovery code stay as they were, so the code the Parent saved still opens the Family.
export const rewrapFamilyKeys = (familyId: string, passphrase: string, keys: FamilyKeys) =>
  wrapBoth(familyId, passphrase, keys.familyKey, keys.recoveryCode);

// Both throw LockError when the secret is wrong; the secret is never part of the message.
export const openWithPassphrase = unwrap;

export const openWithRecoveryCode = (
  familyId: string,
  typedCode: string,
  wrappedKey: string,
  salt: string,
) => unwrap(familyId, formatRecoveryCode(typedCode), wrappedKey, salt);
