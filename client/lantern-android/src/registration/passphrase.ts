export const PASSPHRASE_MIN_LENGTH = 8;
export const PASSPHRASE_MAX_LENGTH = 128;

export const isMismatch = (passphrase: string, again: string) =>
  again !== '' && again !== passphrase;

export const canCreatePassphrase = (passphrase: string, again: string, understood: boolean) =>
  passphrase.length >= PASSPHRASE_MIN_LENGTH && passphrase === again && understood;
