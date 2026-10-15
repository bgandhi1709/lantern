import { ApiError, api } from '../shared/api';
import { createFamilyKeys, makeFamilyId, openWithPassphrase, unlockText } from '../shared/crypto';
import { readValue } from '../shared/storage';
import { emptyChild } from './child';
import { PLACEHOLDER_NOTICE_VERSION } from './notice';
import { registerFamily } from './registerFamily';
import type { RegisterInput } from './registerFamily';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const post = api.post as jest.Mock;

const PASSPHRASE = 'blue lantern 2026';

const makeInput = async (): Promise<RegisterInput> => {
  const id = makeFamilyId();
  return {
    family: { id, keys: await createFamilyKeys(id, PASSPHRASE), passphrase: PASSPHRASE },
    parent: { name: 'Meera Shah', email: 'meera@example.com' },
    region: 'Pune, Maharashtra',
    board: 'ssc',
    children: [
      { name: 'Aarav', classLevel: 4, birthYear: '2017', school: 'St. Mary' },
      { name: 'Mira', classLevel: 2, birthYear: '2020', school: '' },
    ],
  };
};

const sentBody = (call = 0) => post.mock.calls[call][2];

beforeEach(() => post.mockReset().mockResolvedValue({ familyId: 'any' }));

describe('registering the Family', () => {
  it('sends one /v1/register with every personal value locked and the Class plain', async () => {
    const input = await makeInput();

    await registerFamily(input);

    expect(post).toHaveBeenCalledTimes(1);
    expect(post.mock.calls[0][0]).toBe('/v1/register');
    const body = sentBody();
    expect(body).toMatchObject({
      familyId: input.family.id,
      language: 'en',
      region: 'Pune, Maharashtra',
      board: 'ssc',
      consent: { accepted: true, noticeVersion: PLACEHOLDER_NOTICE_VERSION },
      passphraseWrappedKey: input.family.keys.passphraseWrappedKey,
      passphraseSalt: input.family.keys.passphraseSalt,
      recoveryWrappedKey: input.family.keys.recoveryWrappedKey,
      recoverySalt: input.family.keys.recoverySalt,
    });
    const key = input.family.keys.familyKey;
    expect(unlockText(key, body.parentName)).toBe('Meera Shah');
    expect(unlockText(key, body.parentEmail)).toBe('meera@example.com');
    expect(body.children).toHaveLength(2);
    expect(unlockText(key, body.children[0].name)).toBe('Aarav');
    expect(unlockText(key, body.children[0].birthYear)).toBe('2017');
    expect(unlockText(key, body.children[0].school)).toBe('St. Mary');
    expect(body.children[0].classLevel).toBe(4);
    expect(unlockText(key, body.children[1].school)).toBe('');
  });

  it('never puts a name, an email, a birth year or a School in the body as plain text', async () => {
    await registerFamily(await makeInput());

    const text = JSON.stringify(sentBody());
    for (const plain of ['Meera', 'meera@example.com', 'Aarav', 'Mira', '2017', 'St. Mary']) {
      expect(text).not.toContain(plain);
    }
  });

  it('keeps the Family key on the phone once the Family is registered', async () => {
    const input = await makeInput();

    expect(await registerFamily(input)).toBe('registered');

    expect(await readValue('familyKey')).toBe(input.family.keys.familyKey);
  });

  it('goes on to Home when the Family key cannot be stored', async () => {
    const input = await makeInput();
    const setItem = jest.requireMock('expo-secure-store').setItemAsync as jest.Mock;
    setItem.mockRejectedValueOnce(new Error('no'));

    expect(await registerFamily(input)).toBe('registered');
  });

  it('says already-registered and keeps no key, since this phone did not make that Family', async () => {
    post.mockRejectedValue(new ApiError('already-registered', 409));

    expect(await registerFamily(await makeInput())).toBe('alreadyRegistered');

    expect(await readValue('familyKey')).toBeNull();
  });

  it('tries once more with a new Family id when the id is taken, and the Passphrase still opens it', async () => {
    const input = await makeInput();
    post.mockRejectedValueOnce(new ApiError('family-id-taken', 409));

    expect(await registerFamily(input)).toBe('registered');

    expect(post).toHaveBeenCalledTimes(2);
    const retry = sentBody(1);
    expect(retry.familyId).not.toBe(input.family.id);
    expect(
      await openWithPassphrase(
        retry.familyId,
        PASSPHRASE,
        retry.passphraseWrappedKey,
        retry.passphraseSalt,
      ),
    ).toBe(input.family.keys.familyKey);
    expect(await readValue('familyKey')).toBe(input.family.keys.familyKey);
  });

  it('gives up after one retry when the new id is taken too', async () => {
    post.mockRejectedValue(new ApiError('family-id-taken', 409));

    await expect(registerFamily(await makeInput())).rejects.toMatchObject({
      code: 'family-id-taken',
    });
    expect(post).toHaveBeenCalledTimes(2);
  });

  it.each(['class-not-available', 'invalid-registration', 'network'])(
    'passes %s on so the screen can say it',
    async (code) => {
      post.mockRejectedValue(new ApiError(code, 400));

      await expect(registerFamily(await makeInput())).rejects.toMatchObject({ code });
      expect(await readValue('familyKey')).toBeNull();
    },
  );

  it('refuses a Child with no Class', async () => {
    const input = await makeInput();

    await expect(
      registerFamily({ ...input, children: [{ ...emptyChild(), name: 'A' }] }),
    ).rejects.toThrow();
    expect(post).not.toHaveBeenCalled();
  });
});
