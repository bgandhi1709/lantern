import AsyncStorage from '@react-native-async-storage/async-storage';

import { deleteItemAsync, secureStoreContents } from '../../test/fakeSecureStore';
import { clearAll, readValue, writeValue } from './storage';

beforeEach(async () => {
  await AsyncStorage.clear();
});

describe('readValue', () => {
  it('returns the default when nothing is stored', async () => {
    expect(await readValue('welcomeDismissed')).toBe(false);
  });

  it('returns what was written', async () => {
    await writeValue('welcomeDismissed', true);

    expect(await readValue('welcomeDismissed')).toBe(true);
  });

  it('returns the default when the stored text is not valid JSON', async () => {
    await AsyncStorage.setItem('lantern.v1.welcomeDismissed', '{not json');

    expect(await readValue('welcomeDismissed')).toBe(false);
  });

  it('returns the default when the stored value does not match its schema', async () => {
    await AsyncStorage.setItem('lantern.v1.welcomeDismissed', JSON.stringify('yes'));

    expect(await readValue('welcomeDismissed')).toBe(false);
  });

  it('returns the default for a value left by an older version', async () => {
    await AsyncStorage.setItem('lantern.v0.welcomeDismissed', JSON.stringify(true));

    expect(await readValue('welcomeDismissed')).toBe(false);
  });

  it('returns the default when the device store throws', async () => {
    jest.spyOn(AsyncStorage, 'getItem').mockRejectedValueOnce(new Error('disk full'));

    expect(await readValue('welcomeDismissed')).toBe(false);
  });

  it('reads a secure value from the secure store, not the plain one', async () => {
    await writeValue('familyKey', 'k3y');

    expect(secureStoreContents()).toEqual({ 'lantern.v1.familyKey': JSON.stringify('k3y') });
    expect(await AsyncStorage.getAllKeys()).toEqual([]);
    expect(await readValue('familyKey')).toBe('k3y');
  });
});

describe('clearAll', () => {
  it('empties the plain and the secure store, including older versions', async () => {
    await writeValue('welcomeDismissed', true);
    await writeValue('activeChildId', 'child-1');
    await writeValue('familyKey', 'k3y');
    await AsyncStorage.setItem('lantern.v0.welcomeDismissed', 'true');
    await AsyncStorage.setItem('someone.else', 'kept');

    await clearAll();

    expect(await AsyncStorage.getAllKeys()).toEqual(['someone.else']);
    expect(secureStoreContents()).toEqual({});
  });

  it('still clears the plain store when the secure store throws', async () => {
    await writeValue('welcomeDismissed', true);
    deleteItemAsync.mockRejectedValueOnce(new Error('keystore locked'));

    await expect(clearAll()).rejects.toThrow('keystore locked');

    expect(await AsyncStorage.getAllKeys()).toEqual([]);
  });
});

describe('introSeen', () => {
  it('starts false, so a new install plays the full intro, and clearAll forgets it', async () => {
    expect(await readValue('introSeen')).toBe(false);
    await writeValue('introSeen', true);

    await clearAll();

    expect(await readValue('introSeen')).toBe(false);
  });
});
