import { readValue, writeValue } from './storage';

export async function readFamilyKey(uid: string) {
  return (await readValue('familyKeys'))[uid] ?? null;
}

export async function keepFamilyKey(uid: string, familyKey: string) {
  const keys = await readValue('familyKeys');
  await writeValue('familyKeys', { ...keys, [uid]: familyKey });
}
