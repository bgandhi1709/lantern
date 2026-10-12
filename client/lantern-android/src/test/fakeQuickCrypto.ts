import { Buffer } from 'node:buffer';
import * as nodeCrypto from 'node:crypto';

// Stands in for react-native-quick-crypto, which is native. Node has no Argon2 in the version
// Lantern builds with, so the slow derivation is a cheap scrypt here: the tests check how the app
// wraps and locks, not how strong Argon2 is.
export { Buffer };
export const { randomBytes, createCipheriv, createDecipheriv } = nodeCrypto;

type Argon2Params = { message: Uint8Array; nonce: Uint8Array; tagLength: number };

export const argon2 = (
  _algorithm: string,
  params: Argon2Params,
  callback: (error: Error | null, key: Buffer) => void,
) => {
  try {
    callback(
      null,
      nodeCrypto.scryptSync(
        Buffer.from(params.message),
        Buffer.from(params.nonce),
        params.tagLength,
        {
          N: 16,
        },
      ),
    );
  } catch (error) {
    callback(error instanceof Error ? error : new Error('derive failed'), Buffer.alloc(0));
  }
};
