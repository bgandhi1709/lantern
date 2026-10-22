import * as fakeLibrary from '../../test/fakeQuickCrypto';
import {
  createFamilyKeys,
  makeFamilyId,
  openWithRecoveryCode,
  rewrapFamilyKeys,
} from './familyKeys';
import { LockError, lockText, unlockText } from './lock';

describe('the Family id', () => {
  it('is a GUID made on the phone, new every time', () => {
    const id = makeFamilyId();

    expect(id).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
    expect(makeFamilyId()).not.toBe(id);
  });
});

describe('the same key under a new Family id', () => {
  it('keeps the Family key and the Recovery code the Parent emailed, and opens with that code', async () => {
    const oldId = makeFamilyId();
    const newId = makeFamilyId();
    const keys = await createFamilyKeys(oldId);

    const again = await rewrapFamilyKeys(newId, keys);

    expect(again.familyKey).toBe(keys.familyKey);
    expect(again.recoveryCode).toBe(keys.recoveryCode);
    expect(again.recoveryWrappedKey).not.toBe(keys.recoveryWrappedKey);
    expect(
      await openWithRecoveryCode(
        newId,
        keys.recoveryCode,
        again.recoveryWrappedKey,
        again.recoverySalt,
      ),
    ).toBe(keys.familyKey);
  });

  it('no longer opens under the old Family id', async () => {
    const oldId = makeFamilyId();
    const keys = await createFamilyKeys(oldId);
    const again = await rewrapFamilyKeys(makeFamilyId(), keys);

    await expect(
      openWithRecoveryCode(oldId, keys.recoveryCode, again.recoveryWrappedKey, again.recoverySalt),
    ).rejects.toBeInstanceOf(LockError);
  });
});

describe('the Family key and its wrapped copy', () => {
  const familyId = makeFamilyId();

  it('opens with the Recovery code however it is typed', async () => {
    const keys = await createFamilyKeys(familyId);
    const typed = keys.recoveryCode.toLowerCase().replaceAll('-', ' ');

    expect(
      await openWithRecoveryCode(familyId, typed, keys.recoveryWrappedKey, keys.recoverySalt),
    ).toBe(keys.familyKey);
  });

  it('does not open with another Recovery code, and the error does not repeat the code', async () => {
    const keys = await createFamilyKeys(familyId);
    const other = await createFamilyKeys(familyId);

    const failure = await openWithRecoveryCode(
      familyId,
      other.recoveryCode,
      keys.recoveryWrappedKey,
      keys.recoverySalt,
    ).catch((error: unknown) => error);

    expect(failure).toBeInstanceOf(LockError);
    expect(String(failure)).not.toContain(other.recoveryCode);
  });

  it('does not open under another Family id', async () => {
    const keys = await createFamilyKeys(familyId);

    await expect(
      openWithRecoveryCode(
        makeFamilyId(),
        keys.recoveryCode,
        keys.recoveryWrappedKey,
        keys.recoverySalt,
      ),
    ).rejects.toBeInstanceOf(LockError);
  });

  it('makes a new Family key and Recovery code every time', async () => {
    const first = await createFamilyKeys(familyId);
    const second = await createFamilyKeys(familyId);

    expect(second.familyKey).not.toBe(first.familyKey);
    expect(second.recoveryCode).not.toBe(first.recoveryCode);
  });

  it('derives from {family_guid}_{code} with Argon2id at 64 MiB and 3 passes', async () => {
    const derive = jest.spyOn(fakeLibrary, 'argon2');

    const keys = await createFamilyKeys(familyId);

    const sent = derive.mock.calls.map(([algorithm, params]) => ({
      algorithm,
      message: Buffer.from(params.message).toString(),
      memory: (params as { memory?: number }).memory,
      passes: (params as { passes?: number }).passes,
    }));
    expect(sent).toEqual([
      {
        algorithm: 'argon2id',
        message: `${familyId}_${keys.recoveryCode}`,
        memory: 65536,
        passes: 3,
      },
    ]);
  });

  it('fits the limits of /v1/register: the wrapped key and the salt are 1 to 400 characters', async () => {
    const keys = await createFamilyKeys(familyId);

    for (const value of [keys.recoveryWrappedKey, keys.recoverySalt]) {
      expect(value.length).toBeGreaterThanOrEqual(1);
      expect(value.length).toBeLessThanOrEqual(400);
    }
  });

  it('never puts the code or the Family key in what it hands back for the API', async () => {
    const keys = await createFamilyKeys(familyId);
    const forTheApi = JSON.stringify({ a: keys.recoveryWrappedKey, b: keys.recoverySalt });

    expect(forTheApi).not.toContain(keys.recoveryCode);
    expect(forTheApi).not.toContain(keys.familyKey);
  });
});

describe('locked text', () => {
  it('round-trips with the Family key', async () => {
    const keys = await createFamilyKeys(makeFamilyId());

    expect(unlockText(keys.familyKey, lockText(keys.familyKey, 'Aarav'))).toBe('Aarav');
  });
});
