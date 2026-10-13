import { z } from 'zod';

import { ApiError, api } from '../shared/api';
import { lockText, makeFamilyId, rewrapFamilyKeys } from '../shared/crypto';
import type { FamilyKeys } from '../shared/crypto';
import type { Parent } from '../shared/session';
import { writeValue } from '../shared/storage';
import type { Board } from './board';
import type { ChildDraft } from './child';
import { PLACEHOLDER_NOTICE_VERSION } from './notice';

const Registered = z.object({ familyId: z.string() });

export type RegisterInput = {
  family: { id: string; keys: FamilyKeys; passphrase: string };
  parent: Parent;
  region: string;
  board: Board;
  children: ChildDraft[];
};

// 'alreadyRegistered' means another phone made this Parent's Family, so this one has no key to keep.
export type RegisterResult = 'registered' | 'alreadyRegistered';

function lockedChild(familyKey: string, child: ChildDraft) {
  if (child.classLevel === null) throw new Error('A Child needs a Class before registering.');
  return {
    name: lockText(familyKey, child.name),
    birthYear: lockText(familyKey, child.birthYear),
    school: lockText(familyKey, child.school),
    classLevel: child.classLevel,
  };
}

function bodyFor(input: RegisterInput, familyId: string, keys: FamilyKeys) {
  return {
    familyId,
    language: 'en',
    consent: { accepted: true, noticeVersion: PLACEHOLDER_NOTICE_VERSION },
    region: input.region,
    board: input.board,
    parentName: lockText(keys.familyKey, input.parent.name),
    parentEmail: lockText(keys.familyKey, input.parent.email),
    passphraseWrappedKey: keys.passphraseWrappedKey,
    passphraseSalt: keys.passphraseSalt,
    recoveryWrappedKey: keys.recoveryWrappedKey,
    recoverySalt: keys.recoverySalt,
    children: input.children.map((child) => lockedChild(keys.familyKey, child)),
  };
}

const hasCode = (error: unknown, code: string) => error instanceof ApiError && error.code === code;

// A phone that registered must be able to open its Family again, but a phone with no working
// storage still has the Passphrase, so a failed write does not undo the registration.
async function keepFamilyKey(familyKey: string) {
  try {
    await writeValue('familyKey', familyKey);
  } catch {
    // The Passphrase opens the Family on the next start.
  }
}

async function send(input: RegisterInput, familyId: string, keys: FamilyKeys) {
  await api.post('/v1/register', Registered, bodyFor(input, familyId, keys));
}

// One call carries the Family and every Child, so a Family is never half made. A taken Family id
// is tried once more with a new one; the Passphrase is still in the draft, so the Parent sees nothing.
export async function registerFamily(input: RegisterInput): Promise<RegisterResult> {
  const { id, keys, passphrase } = input.family;
  try {
    await send(input, id, keys);
    await keepFamilyKey(keys.familyKey);
    return 'registered';
  } catch (error) {
    if (hasCode(error, 'already-registered')) return 'alreadyRegistered';
    if (!hasCode(error, 'family-id-taken')) throw error;
  }

  const newId = makeFamilyId();
  const newKeys = await rewrapFamilyKeys(newId, passphrase, keys);
  try {
    await send(input, newId, newKeys);
  } catch (error) {
    if (hasCode(error, 'already-registered')) return 'alreadyRegistered';
    throw error;
  }
  await keepFamilyKey(newKeys.familyKey);
  return 'registered';
}
