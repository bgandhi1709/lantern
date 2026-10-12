import * as fakeLibrary from '../../test/fakeQuickCrypto';
import {
  createFamilyKeys,
  makeFamilyId,
  openWithPassphrase,
  openWithRecoveryCode,
} from './familyKeys';
import { LockError, lockText, unlockText } from './lock';

const PASSPHRASE = 'blue lantern 2026';

describe('the Family id', () => {
  it('is a GUID made on the phone, new every time', () => {
    const id = makeFamilyId();

    expect(id).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
    expect(makeFamilyId()).not.toBe(id);
  });
});

describe('the Family key and its two wrapped copies', () => {
  const familyId = makeFamilyId();

  it('opens with the Passphrase, and with the Recovery code however it is typed', async () => {
    const keys = await createFamilyKeys(familyId, PASSPHRASE);

    expect(
      await openWithPassphrase(
        familyId,
        PASSPHRASE,
        keys.passphraseWrappedKey,
        keys.passphraseSalt,
      ),
    ).toBe(keys.familyKey);
    const typed = keys.recoveryCode.toLowerCase().replaceAll('-', ' ');
    expect(
      await openWithRecoveryCode(familyId, typed, keys.recoveryWrappedKey, keys.recoverySalt),
    ).toBe(keys.familyKey);
  });

  it('locks values that the opened key unlocks', async () => {
    const keys = await createFamilyKeys(familyId, PASSPHRASE);
    const reopened = await openWithPassphrase(
      familyId,
      PASSPHRASE,
      keys.passphraseWrappedKey,
      keys.passphraseSalt,
    );

    expect(unlockText(reopened, lockText(keys.familyKey, 'Aarav'))).toBe('Aarav');
  });

  it('does not open with a wrong Passphrase, a wrong code, or another Family id', async () => {
    const keys = await createFamilyKeys(familyId, PASSPHRASE);
    const { passphraseWrappedKey: wrapped, passphraseSalt: salt } = keys;

    await expect(openWithPassphrase(familyId, 'blue lantern 2027', wrapped, salt)).rejects.toThrow(
      LockError,
    );
    await expect(openWithPassphrase(makeFamilyId(), PASSPHRASE, wrapped, salt)).rejects.toThrow(
      LockError,
    );
    await expect(
      openWithRecoveryCode(familyId, 'AAAA', keys.recoveryWrappedKey, keys.recoverySalt),
    ).rejects.toThrow(LockError);
  });

  it('fits the limits of /v1/register: each wrapped key and salt is 1 to 400 characters', async () => {
    const keys = await createFamilyKeys(familyId, PASSPHRASE);

    for (const value of [
      keys.passphraseWrappedKey,
      keys.passphraseSalt,
      keys.recoveryWrappedKey,
      keys.recoverySalt,
    ]) {
      expect(value.length).toBeGreaterThanOrEqual(1);
      expect(value.length).toBeLessThanOrEqual(400);
    }
  });

  it('uses its own random salt for each wrap, and a new key and code each time', async () => {
    const first = await createFamilyKeys(familyId, PASSPHRASE);
    const second = await createFamilyKeys(familyId, PASSPHRASE);

    expect(first.passphraseSalt).not.toBe(first.recoverySalt);
    expect(first.passphraseSalt).not.toBe(second.passphraseSalt);
    expect(first.familyKey).not.toBe(second.familyKey);
    expect(first.recoveryCode).not.toBe(second.recoveryCode);
  });

  it('derives from {family_guid}_{secret} with Argon2id at 64 MiB and 3 passes', async () => {
    const derive = jest.spyOn(fakeLibrary, 'argon2');

    const keys = await createFamilyKeys(familyId, PASSPHRASE);

    const sent = derive.mock.calls.map(([algorithm, params]) => ({
      algorithm,
      message: Buffer.from(params.message).toString(),
      memory: (params as { memory?: number }).memory,
      passes: (params as { passes?: number }).passes,
    }));
    expect(sent).toEqual([
      { algorithm: 'argon2id', message: `${familyId}_${PASSPHRASE}`, memory: 65536, passes: 3 },
      {
        algorithm: 'argon2id',
        message: `${familyId}_${keys.recoveryCode}`,
        memory: 65536,
        passes: 3,
      },
    ]);
  });

  it('never puts the Passphrase or the code in what it hands back for the API', async () => {
    const keys = await createFamilyKeys(familyId, PASSPHRASE);
    const forTheApi = JSON.stringify({
      a: keys.passphraseWrappedKey,
      b: keys.passphraseSalt,
      c: keys.recoveryWrappedKey,
      d: keys.recoverySalt,
    });

    expect(forTheApi).not.toContain(PASSPHRASE);
    expect(forTheApi).not.toContain(keys.recoveryCode);
  });
});
