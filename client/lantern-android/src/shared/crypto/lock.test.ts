import { LockError, lockText, makeFamilyKey, unlockText } from './lock';

describe('locking a value with the Family key', () => {
  const key = makeFamilyKey();

  it.each([
    ['a name', 'Aarav Sharma'],
    ['a Gujarati name', 'આરવ શર્મા'],
    ['an empty School', ''],
  ])('unlocks %s to exactly what was locked', (_, text) => {
    expect(unlockText(key, lockText(key, text))).toBe(text);
  });

  it('is versioned text that does not hold the original', () => {
    const locked = lockText(key, 'Aarav Sharma');

    expect(locked).toMatch(/^v1\.[A-Za-z0-9+/]+=*$/);
    expect(locked).not.toContain('Aarav');
  });

  it('gives a different result each time, so equal names cannot be matched', () => {
    expect(lockText(key, 'Aarav')).not.toBe(lockText(key, 'Aarav'));
  });

  it('stays within the 400 characters the API accepts, even for an empty value', () => {
    expect(lockText(key, '').length).toBeLessThanOrEqual(400);
    expect(lockText(key, 'a'.repeat(269)).length).toBeLessThanOrEqual(400);
  });

  it('refuses a value too long to fit, rather than send something the API will reject', () => {
    expect(() => lockText(key, 'a'.repeat(270))).toThrow(LockError);
  });

  it('refuses to unlock with a different key or after a change', () => {
    const locked = lockText(key, 'Aarav');
    const changed = `${locked.slice(0, -4)}AAAA`;

    expect(() => unlockText(makeFamilyKey(), locked)).toThrow(LockError);
    expect(() => unlockText(key, changed)).toThrow(LockError);
  });

  it('refuses text that is not a locked value, without echoing it', () => {
    expect(() => unlockText(key, 'Aarav Sharma')).toThrow(LockError);
    expect(() => unlockText(key, 'v9.AAAA')).toThrow(LockError);
    expect(() => unlockText(key, 'Aarav Sharma')).not.toThrow(/Aarav/);
  });
});
