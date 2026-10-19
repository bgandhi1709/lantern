import { keepFamilyKey, readFamilyKey } from './familyKeyStore';
import { clearAll } from './storage';

beforeEach(clearAll);

describe('the Family key kept on this phone', () => {
  it('is found again for the same account', async () => {
    await keepFamilyKey('parent-a', 'key-a');

    expect(await readFamilyKey('parent-a')).toBe('key-a');
  });

  it('is not given to another account', async () => {
    await keepFamilyKey('parent-a', 'key-a');

    expect(await readFamilyKey('parent-b')).toBeNull();
  });

  it('keeps a second account’s key beside the first', async () => {
    await keepFamilyKey('parent-a', 'key-a');
    await keepFamilyKey('parent-b', 'key-b');

    expect(await readFamilyKey('parent-a')).toBe('key-a');
    expect(await readFamilyKey('parent-b')).toBe('key-b');
  });

  it('is forgotten only when the phone’s storage is cleared', async () => {
    await keepFamilyKey('parent-a', 'key-a');

    await clearAll();

    expect(await readFamilyKey('parent-a')).toBeNull();
  });
});
