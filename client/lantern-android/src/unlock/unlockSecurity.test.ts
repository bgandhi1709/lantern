import type { z } from 'zod';

import { ApiError, api } from '../shared/api';
import { LockError, createFamilyKeys, makeFamilyId } from '../shared/crypto';
import { clearAll, readFamilyKey, readValue } from '../shared/storage';
import { secureStoreContents } from '../test/fakeSecureStore';
import { startAgain } from './startAgain';
import { UnlockWaitError, fetchFamilyLock, unlockFamily } from './unlockFamily';
import type { FamilyLock } from './unlockFamily';
import { waitSeconds } from './unlockWait';

// The lint rule keeps the real store module out of everything but src/shared/storage; this is the
// same in-memory fake jest.setup.ts installs, read here only to look at what the phone holds.
const AsyncStorage = jest.requireActual(
  '@react-native-async-storage/async-storage/jest/async-storage-mock',
);

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const get = api.get as jest.Mock;
const post = api.post as jest.Mock;
const put = api.put as jest.Mock;
const del = api.delete as jest.Mock;

const UID = 'parent-a';
const WRONG = 'ABCD-EFGH-JKLM-NPQR';
const NOW = 1_000_000;

let lock: FamilyLock;
let recoveryCode: string;
let familyKey: string;

beforeAll(async () => {
  const familyId = makeFamilyId();
  const keys = await createFamilyKeys(familyId);
  recoveryCode = keys.recoveryCode;
  familyKey = keys.familyKey;
  lock = {
    familyId,
    recoveryWrappedKey: keys.recoveryWrappedKey,
    recoverySalt: keys.recoverySalt,
  };
});

const consoleMethods = ['log', 'info', 'warn', 'error', 'debug'] as const;
const consoleSpies = consoleMethods.map((method) =>
  jest.spyOn(console, method).mockImplementation(() => undefined),
);

beforeEach(async () => {
  for (const mock of [get, post, put, del]) mock.mockReset();
  consoleSpies.forEach((spy) => spy.mockClear());
  await clearAll();
});

const requestsOf = (mock: jest.Mock, hasBody: boolean) =>
  mock.mock.calls.map((call) => JSON.stringify(hasBody ? [call[0], call[2]] : [call[0]]));

const apiCalls = () => [
  ...requestsOf(get, false),
  ...requestsOf(post, true),
  ...requestsOf(put, true),
  ...requestsOf(del, false),
];

const plainStorage = async () => {
  const keys = await AsyncStorage.getAllKeys();
  return JSON.stringify(await AsyncStorage.multiGet(keys));
};

const everythingOnThePhone = async () =>
  JSON.stringify([await plainStorage(), secureStoreContents()]);

const everythingLogged = () => JSON.stringify(consoleSpies.flatMap((spy) => spy.mock.calls));

const failedWith = async (attempt: Promise<unknown>) => {
  try {
    await attempt;
  } catch (error) {
    return error;
  }
  throw new Error('The attempt was expected to fail.');
};

describe('asking the API for the locked Family', () => {
  it('sends one GET /v1/me with no query and no body, and nothing else', async () => {
    get.mockResolvedValue(lock);

    expect(await fetchFamilyLock()).toEqual(lock);

    expect(get).toHaveBeenCalledTimes(1);
    expect(get.mock.calls[0][0]).toBe('/v1/me');
    expect(get.mock.calls[0]).toHaveLength(2);
    expect([post, put, del].every((mock) => mock.mock.calls.length === 0)).toBe(true);
  });

  it('gives the Parent’s own copy only: wrapped keys and salts, no names', async () => {
    const answer = { ...lock, children: [{ childId: 'c1', name: 'v1.locked' }], region: 'Pune' };
    get.mockImplementation(async (_path: string, schema: z.ZodType) => schema.parse(answer));

    const received = await fetchFamilyLock();

    expect(Object.keys(received).sort()).toEqual(Object.keys(lock).sort());
  });
});

describe('opening the Family on this phone', () => {
  it('opens with the Recovery code and keeps only the Family key, for this account', async () => {
    await unlockFamily(lock, { uid: UID, typed: recoveryCode, now: NOW });

    expect(await readFamilyKey(UID)).toBe(familyKey);
    expect(await readFamilyKey('someone-else')).toBeNull();
  });

  it('opens with the Recovery code however it is typed', async () => {
    const typed = recoveryCode.replace(/-/g, ' ').toLowerCase();

    await unlockFamily(lock, { uid: UID, typed, now: NOW });

    expect(await readFamilyKey(UID)).toBe(familyKey);
  });

  it('rejects a wrong code with a LockError and keeps nothing', async () => {
    const error = await failedWith(unlockFamily(lock, { uid: UID, typed: WRONG, now: NOW }));

    expect(error).toBeInstanceOf(LockError);
    expect(await readFamilyKey(UID)).toBeNull();
  });
});

describe('a Recovery code from someone else’s Family', () => {
  it('does not open this Parent’s own Family and keeps no key', async () => {
    const strangerFamilyId = makeFamilyId();
    const stranger = await createFamilyKeys(strangerFamilyId);

    const error = await failedWith(
      unlockFamily(lock, { uid: UID, typed: stranger.recoveryCode, now: NOW }),
    );

    expect(error).toBeInstanceOf(LockError);
    expect(await readFamilyKey(UID)).toBeNull();
    expect(await readValue('unlockWait')).toEqual({ failures: 1, until: NOW + 5_000 });
  });

  it('does not open this Family from another Family’s wrapped key and salt either', async () => {
    const stranger = await createFamilyKeys(makeFamilyId());
    const strangerLock = {
      familyId: lock.familyId,
      recoveryWrappedKey: stranger.recoveryWrappedKey,
      recoverySalt: stranger.recoverySalt,
    };

    const error = await failedWith(
      unlockFamily(strangerLock, { uid: UID, typed: recoveryCode, now: NOW }),
    );

    expect(error).toBeInstanceOf(LockError);
    expect(await readFamilyKey(UID)).toBeNull();
  });
});

describe('what a locked phone sends to the API', () => {
  it('sends nothing at all while the Parent tries values, right or wrong', async () => {
    await failedWith(unlockFamily(lock, { uid: UID, typed: WRONG, now: NOW }));
    await unlockFamily(lock, {
      uid: UID,
      typed: recoveryCode,
      now: NOW + 600_000,
    });

    for (const mock of [get, post, put, del]) expect(mock).not.toHaveBeenCalled();
  });

  it('never puts a typed value, a code or the Family key in any request it makes', async () => {
    get.mockResolvedValue(lock);
    del.mockResolvedValue(undefined);

    await fetchFamilyLock();
    await unlockFamily(lock, { uid: UID, typed: recoveryCode, now: NOW });
    await startAgain();

    const sent = apiCalls().join('\n');
    for (const secret of [WRONG, recoveryCode, recoveryCode.replace(/-/g, ''), familyKey]) {
      expect(sent).not.toContain(secret);
    }
    expect(apiCalls()).toEqual(['["/v1/me"]', '["/v1/family"]']);
  });
});

describe('what a locked phone writes down', () => {
  it('stores the Family key and the wait, and never a typed value or a code', async () => {
    await failedWith(unlockFamily(lock, { uid: UID, typed: WRONG, now: NOW }));
    await unlockFamily(lock, {
      uid: UID,
      typed: recoveryCode,
      now: NOW + 600_000,
    });

    const stored = await everythingOnThePhone();
    for (const secret of [WRONG, recoveryCode, recoveryCode.replace(/-/g, '')]) {
      expect(stored).not.toContain(secret);
    }
    expect(stored).toContain(familyKey);
  });

  it('never logs a typed value, a code or the Family key, on success or on failure', async () => {
    await failedWith(unlockFamily(lock, { uid: UID, typed: WRONG, now: NOW }));
    await unlockFamily(lock, {
      uid: UID,
      typed: recoveryCode,
      now: NOW + 600_000,
    });

    const logged = everythingLogged();
    for (const secret of [WRONG, recoveryCode, familyKey]) {
      expect(logged).not.toContain(secret);
    }
  });

  it('puts no typed value or key in an error message', async () => {
    const error = await failedWith(unlockFamily(lock, { uid: UID, typed: WRONG, now: NOW }));

    expect(String(error)).not.toContain(WRONG);
    expect(String(error)).not.toContain(familyKey);
  });
});

describe('guessing from a phone that holds the Parent’s Family', () => {
  it('waits 5 seconds after the first wrong try, doubling each time up to 5 minutes', () => {
    expect([1, 2, 3, 4, 5, 6, 7, 8, 9, 50].map(waitSeconds)).toEqual([
      5, 10, 20, 40, 80, 160, 300, 300, 300, 300,
    ]);
    expect(waitSeconds(0)).toBe(0);
  });

  it('refuses a try during the wait without working out a key, even with the right value', async () => {
    await failedWith(unlockFamily(lock, { uid: UID, typed: WRONG, now: NOW }));

    const error = await failedWith(
      unlockFamily(lock, { uid: UID, typed: recoveryCode, now: NOW + 4_000 }),
    );

    expect(error).toBeInstanceOf(UnlockWaitError);
    expect((error as UnlockWaitError).secondsLeft).toBe(1);
    expect(await readFamilyKey(UID)).toBeNull();
  });

  it('lets the Parent try again once the wait is over, and the next wait is twice as long', async () => {
    await failedWith(unlockFamily(lock, { uid: UID, typed: WRONG, now: NOW }));
    await failedWith(unlockFamily(lock, { uid: UID, typed: WRONG, now: NOW + 5_000 }));

    expect(await readValue('unlockWait')).toEqual({ failures: 2, until: NOW + 5_000 + 10_000 });
  });

  it('keeps the wait when the app is closed and opened again', async () => {
    await failedWith(unlockFamily(lock, { uid: UID, typed: WRONG, now: NOW }));

    const stillWaiting = await failedWith(
      unlockFamily(lock, { uid: UID, typed: recoveryCode, now: NOW + 1_000 }),
    );

    expect(stillWaiting).toBeInstanceOf(UnlockWaitError);
  });

  it('clears the wait once the Family is open', async () => {
    await failedWith(unlockFamily(lock, { uid: UID, typed: WRONG, now: NOW }));
    await unlockFamily(lock, { uid: UID, typed: recoveryCode, now: NOW + 5_000 });

    expect(await readValue('unlockWait')).toEqual({ failures: 0, until: 0 });
  });
});

describe('Start again', () => {
  it('deletes the Family with one DELETE /v1/family, then wipes this phone', async () => {
    del.mockResolvedValue(undefined);
    await unlockFamily(lock, { uid: UID, typed: recoveryCode, now: NOW });
    await AsyncStorage.setItem('lantern.v1.theme', JSON.stringify('dark'));

    await startAgain();

    expect(del).toHaveBeenCalledTimes(1);
    expect(del.mock.calls[0][0]).toBe('/v1/family');
    expect(await readFamilyKey(UID)).toBeNull();
    expect(await plainStorage()).toBe('[]');
    expect(secureStoreContents()).toEqual({});
  });

  it('leaves the phone as it was when the API cannot delete, and says so', async () => {
    del.mockRejectedValue(new ApiError('unreachable', null));
    await unlockFamily(lock, { uid: UID, typed: recoveryCode, now: NOW });

    const error = await failedWith(startAgain());

    expect(error).toBeInstanceOf(ApiError);
    expect(await readFamilyKey(UID)).toBe(familyKey);
  });

  it('also clears a wait left by wrong tries, so a new Family starts with none', async () => {
    del.mockResolvedValue(undefined);
    await failedWith(unlockFamily(lock, { uid: UID, typed: WRONG, now: NOW }));

    await startAgain();

    expect(await readValue('unlockWait')).toEqual({ failures: 0, until: 0 });
  });
});
