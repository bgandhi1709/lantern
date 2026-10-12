import { randomBytes } from './native';

// 32 letters and digits without the look-alikes I, O, 0 and 1: 16 of them are 80 bits.
const ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
const CODE_LENGTH = 16;
const GROUP_LENGTH = 4;

export function makeRecoveryCode() {
  const letters = Array.from(randomBytes(CODE_LENGTH), (byte) => ALPHABET[byte % ALPHABET.length]);
  return formatRecoveryCode(letters.join(''));
}

// Typed in any case, with or without spaces and dashes, a code reads the same.
export function formatRecoveryCode(typed: string) {
  const letters = typed.replace(/[\s-]/g, '').toUpperCase();
  const groups = letters.match(new RegExp(`.{1,${GROUP_LENGTH}}`, 'g')) ?? [];
  return groups.join('-');
}
