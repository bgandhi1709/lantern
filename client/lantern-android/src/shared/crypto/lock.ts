import { fromBase64, open, randomBytes, seal, toBase64, toBytes, toText } from './native';

// A locked value is "v1." and the base64 of the sealed bytes. The version fixes the cipher
// (AES-256-GCM) and, for wrapped keys, the Argon2id cost, so a stronger setting becomes "v2"
// while old Families still open.
const VERSION_PREFIX = 'v1.';
const FAMILY_KEY_BYTES = 32;

// What the API accepts for any locked value (1 to 400 characters).
export const LOCKED_MAX_LENGTH = 400;

export class LockError extends Error {}

// The Family key as text, the form it is kept in on the phone.
export const makeFamilyKey = () => toBase64(randomBytes(FAMILY_KEY_BYTES));

export function lockBytes(key: Uint8Array, plain: Uint8Array) {
  const locked = `${VERSION_PREFIX}${toBase64(seal(key, plain))}`;
  if (locked.length > LOCKED_MAX_LENGTH) throw new LockError('The value is too long to lock.');
  return locked;
}

// The message never holds the locked text or the result, so a log line cannot leak either.
export function unlockBytes(key: Uint8Array, locked: string) {
  if (!locked.startsWith(VERSION_PREFIX)) throw new LockError('This is not a locked value.');
  try {
    return open(key, fromBase64(locked.slice(VERSION_PREFIX.length)));
  } catch {
    throw new LockError('The value could not be unlocked.');
  }
}

export const lockText = (familyKey: string, text: string) =>
  lockBytes(fromBase64(familyKey), toBytes(text));

export const unlockText = (familyKey: string, locked: string) =>
  toText(unlockBytes(fromBase64(familyKey), locked));
