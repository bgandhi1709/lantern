import {
  Buffer,
  argon2,
  createCipheriv,
  createDecipheriv,
  randomBytes as nativeRandomBytes,
} from 'react-native-quick-crypto';

// The only file that touches the crypto library; everything else works on bytes and strings.

const NONCE_BYTES = 12;
const TAG_BYTES = 16;
const KEY_BYTES = 32;

export type KeyCost = { memoryKiB: number; passes: number };

export const randomBytes = (count: number) => new Uint8Array(nativeRandomBytes(count));

export const toBase64 = (bytes: Uint8Array) => Buffer.from(bytes).toString('base64');
export const fromBase64 = (text: string) => new Uint8Array(Buffer.from(text, 'base64'));
export const toBytes = (text: string) => new Uint8Array(Buffer.from(text, 'utf8'));
export const toText = (bytes: Uint8Array) => Buffer.from(bytes).toString('utf8');

// Argon2id is slow on purpose, so the call is asynchronous and never blocks the screen.
export function deriveKey(secret: string, salt: Uint8Array, cost: KeyCost) {
  return new Promise<Uint8Array>((resolve, reject) => {
    argon2(
      'argon2id',
      {
        message: toBytes(secret),
        nonce: salt,
        parallelism: 1,
        tagLength: KEY_BYTES,
        memory: cost.memoryKiB,
        passes: cost.passes,
      },
      (error, key) => (error ? reject(error) : resolve(new Uint8Array(key))),
    );
  });
}

// AES-256-GCM. The result is the nonce, the scrambled bytes and the tag that proves nobody changed them.
export function seal(key: Uint8Array, plain: Uint8Array) {
  const nonce = randomBytes(NONCE_BYTES);
  const cipher = createCipheriv('aes-256-gcm', key, nonce);
  const scrambled = Buffer.concat([cipher.update(plain), cipher.final()]);
  return new Uint8Array(Buffer.concat([nonce, scrambled, cipher.getAuthTag()]));
}

// Throws when the key is wrong or the bytes were changed.
export function open(key: Uint8Array, sealed: Uint8Array) {
  const nonce = sealed.subarray(0, NONCE_BYTES);
  const tag = sealed.subarray(sealed.length - TAG_BYTES);
  const scrambled = sealed.subarray(NONCE_BYTES, sealed.length - TAG_BYTES);
  const decipher = createDecipheriv('aes-256-gcm', key, nonce);
  decipher.setAuthTag(Buffer.from(tag));
  return new Uint8Array(Buffer.concat([decipher.update(scrambled), decipher.final()]));
}
