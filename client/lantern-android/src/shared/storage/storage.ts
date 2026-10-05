import AsyncStorage from '@react-native-async-storage/async-storage';
import * as SecureStore from 'expo-secure-store';

import { KEY_PREFIX, storageKey, storedValues } from './keys';
import type { StoredName, StoredValue } from './keys';

const secureNames = (Object.keys(storedValues) as StoredName[]).filter(
  (name) => storedValues[name].secure,
);

async function readRaw(name: StoredName) {
  const key = storageKey(name);
  return storedValues[name].secure ? SecureStore.getItemAsync(key) : AsyncStorage.getItem(key);
}

// The app must work without storage, so any failure to read gives the default.
export async function readValue<N extends StoredName>(name: N): Promise<StoredValue<N>> {
  const { schema, fallback } = storedValues[name];
  try {
    const raw = await readRaw(name);
    if (raw === null) return fallback;
    const parsed = schema.safeParse(JSON.parse(raw));
    return parsed.success ? parsed.data : fallback;
  } catch {
    return fallback;
  }
}

export async function writeValue<N extends StoredName>(name: N, value: StoredValue<N>) {
  const key = storageKey(name);
  const raw = JSON.stringify(storedValues[name].schema.parse(value));
  if (storedValues[name].secure) await SecureStore.setItemAsync(key, raw);
  else await AsyncStorage.setItem(key, raw);
}

async function clearPlain() {
  const keys = await AsyncStorage.getAllKeys();
  await AsyncStorage.multiRemove(keys.filter((key) => key.startsWith(KEY_PREFIX)));
}

async function clearSecure() {
  await Promise.all(secureNames.map((name) => SecureStore.deleteItemAsync(storageKey(name))));
}

export async function clearAll() {
  const results = await Promise.allSettled([clearPlain(), clearSecure()]);
  const failed = results.find((result) => result.status === 'rejected');
  if (failed) throw failed.reason;
}
