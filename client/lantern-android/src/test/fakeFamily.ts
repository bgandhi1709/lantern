import type { z } from 'zod';

import { createFamilyKeys, lockText, makeFamilyId } from '../shared/crypto';
import { writeValue } from '../shared/storage';

export const AARYA = '11111111-1111-4111-8111-111111111111';
export const ROHAN = '22222222-2222-4222-8222-222222222222';

export const AARYA_SUBJECTS = {
  items: [
    { subjectId: 'science', name: 'Science', icon: 'science', chapterCount: 12 },
    { subjectId: 'mathematics', name: 'Mathematics', icon: 'maths', chapterCount: 14 },
  ],
  nextCursor: null,
};

export const ROHAN_SUBJECTS = {
  items: [{ subjectId: 'english', name: 'English', icon: 'english', chapterCount: 4 }],
  nextCursor: null,
};

const SUBJECTS: Record<string, unknown> = { [AARYA]: AARYA_SUBJECTS, [ROHAN]: ROHAN_SUBJECTS };

// A Family registered on this phone: the Family key is on the device and the API answers like the real one.
export async function registerFamilyOnThisPhone() {
  const { familyKey } = await createFamilyKeys(makeFamilyId(), 'blue lantern 2026');
  await writeValue('familyKey', familyKey);
  return familyKey;
}

// Parses with the schema the screen passes, as the real client does, so a response the screen could not read fails here too.
export function answerLikeTheApi(get: jest.Mock, familyKey: string) {
  get.mockImplementation(async (path: string, schema: z.ZodType) => {
    if (path === '/v1/me') {
      return schema.parse({
        children: [
          { childId: AARYA, name: lockText(familyKey, 'Aarya'), classLevel: 6 },
          { childId: ROHAN, name: lockText(familyKey, 'Rohan'), classLevel: 3 },
        ],
      });
    }
    return schema.parse(SUBJECTS[path.split('/')[3]]);
  });
}
