export const PASSPHRASE_MIN_LENGTH = 8;
export const PASSPHRASE_MAX_LENGTH = 128;

// Typing the second one correctly passes through every start of the first, so only a start that
// differs counts as a mismatch.
export const isMismatch = (passphrase: string, again: string) => !passphrase.startsWith(again);

// Spaces alone are not a passphrase.
export const canCreatePassphrase = (passphrase: string, again: string, understood: boolean) =>
  passphrase.trim().length >= PASSPHRASE_MIN_LENGTH && passphrase === again && understood;
