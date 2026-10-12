import { lockBytes, makeFamilyKey, unlockBytes } from './lock';
import { deriveKey, fromBase64, randomBytes, toBase64 } from './native';
import type { KeyCost } from './native';
import { formatRecoveryCode, makeRecoveryCode } from './recoveryCode';

// The cost of "v1" wrapped keys (see lock.ts): 64 MiB and 3 passes take about a second on a
// phone, and make a stolen wrapped key slow to guess. Changing it means a new version.
const WRAP_COST: KeyCost = { memoryKiB: 65_536, passes: 3 };
const SALT_BYTES = 16;
const UUID_VERSION_4 = 0x40;
const UUID_VARIANT = 0x80;

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

export function makeFamilyId() {
  const bytes = randomBytes(16);
  bytes[6] = (bytes[6] & 0x0f) | UUID_VERSION_4;
  bytes[8] = (bytes[8] & 0x3f) | UUID_VARIANT;
  const hex = Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('');
  return [0, 8, 12, 16, 20].map((start, i, all) => hex.slice(start, all[i + 1])).join('-');
}

// The Family id is part of the input, so two Families with the same Passphrase get different keys.
const wrappingKey = (familyId: string, secret: string, salt: Uint8Array) =>
  deriveKey(`${familyId}_${secret}`, salt, WRAP_COST);

async function wrap(familyKey: string, familyId: string, secret: string) {
  const salt = randomBytes(SALT_BYTES);
  const key = await wrappingKey(familyId, secret, salt);
  return { wrappedKey: lockBytes(key, fromBase64(familyKey)), salt: toBase64(salt) };
}

async function unwrap(familyId: string, secret: string, wrappedKey: string, salt: string) {
  const key = await wrappingKey(familyId, secret, fromBase64(salt));
  return toBase64(unlockBytes(key, wrappedKey));
}

export async function createFamilyKeys(familyId: string, passphrase: string): Promise<FamilyKeys> {
  const familyKey = makeFamilyKey();
  const recoveryCode = makeRecoveryCode();
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

// Both throw LockError when the secret is wrong; the secret is never part of the message.
export const openWithPassphrase = unwrap;

export const openWithRecoveryCode = (
  familyId: string,
  typedCode: string,
  wrappedKey: string,
  salt: string,
) => unwrap(familyId, formatRecoveryCode(typedCode), wrappedKey, salt);
