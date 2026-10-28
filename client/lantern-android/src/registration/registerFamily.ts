import { z } from 'zod';

import { ApiError, api } from '../shared/api';
import { lockText, makeFamilyId, rewrapFamilyKeys } from '../shared/crypto';
import type { FamilyKeys } from '../shared/crypto';
import type { Parent } from '../shared/session';
import { keepFamilyKey } from '../shared/storage';
import type { Board } from './board';
import type { ChildDraft } from './child';
import { PLACEHOLDER_NOTICE_VERSION } from './notice';

const Registered = z.object({ familyId: z.string() });

export type RegisterInput = {
  family: { id: string; keys: FamilyKeys };
  parent: Parent;
  region: string;
  board: Board;
  children: ChildDraft[];
};

// 'alreadyRegistered' means another phone made this Parent's Family, so this one has no key to keep.
export type RegisterResult = 'registered' | 'alreadyRegistered';

export function lockedChild(familyKey: string, child: ChildDraft) {
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
    recoveryWrappedKey: keys.recoveryWrappedKey,
    recoverySalt: keys.recoverySalt,
    children: input.children.map((child) => lockedChild(keys.familyKey, child)),
  };
}

const hasCode = (error: unknown, code: string) => error instanceof ApiError && error.code === code;

const keepKeyForParent = (uid: string, familyKey: string) =>
  keepFamilyKey(uid, familyKey).catch(() => undefined);

async function send(input: RegisterInput, familyId: string, keys: FamilyKeys) {
  await api.post('/v1/register', Registered, bodyFor(input, familyId, keys));
}

export async function registerFamily(input: RegisterInput): Promise<RegisterResult> {
  const { id, keys } = input.family;
  try {
    await send(input, id, keys);
    await keepKeyForParent(input.parent.uid, keys.familyKey);
    return 'registered';
  } catch (error) {
    if (hasCode(error, 'already-registered')) return 'alreadyRegistered';
    if (!hasCode(error, 'family-id-taken')) throw error;
  }

  const newId = makeFamilyId();
  const newKeys = await rewrapFamilyKeys(newId, keys);
  try {
    await send(input, newId, newKeys);
  } catch (error) {
    if (hasCode(error, 'already-registered')) return 'alreadyRegistered';
    throw error;
  }
  await keepKeyForParent(input.parent.uid, newKeys.familyKey);
  return 'registered';
}
