import type { z } from 'zod';

import { createFamilyKeys, lockText, makeFamilyId } from '../shared/crypto';
import { keepFamilyKey } from '../shared/storage';
import { FAKE_UID, fakeRestoredSession } from './fakeFirebaseAuth';

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

const chaptersOf = (subjectId: string, titles: [string, string][]) => ({
  items: titles.map(([title, summary], index) => ({
    chapterId: `${subjectId}-${index + 1}`,
    number: index + 1,
    title,
    summary,
  })),
  nextCursor: null,
});

const CHAPTERS: Record<string, unknown> = {
  [`${AARYA}/science`]: chaptersOf('science', [
    ['Materials Around Us', 'The different materials around us and how we choose one for a job.'],
    ['Changes of State', 'How materials change between solid, liquid and gas.'],
    ['Measurement of Length and Motion', 'How we measure length and tell whether something moves.'],
  ]),
  [`${AARYA}/mathematics`]: chaptersOf('mathematics', [
    ['Patterns in Mathematics', 'Finding and continuing patterns in numbers and shapes.'],
    ['Lines and Angles', 'Straight lines, how they meet, and the angle between them.'],
  ]),
  [`${ROHAN}/english`]: chaptersOf('english', [
    ['Poonam’s Day Out', 'A day spent noticing the animals, plants and people around us.'],
    ['The Plant Fairy', 'How a seed grows into a plant, told as a story.'],
  ]),
};

export async function registerFamilyOnThisPhone() {
  const { familyKey } = await createFamilyKeys(makeFamilyId());
  fakeRestoredSession();
  await keepFamilyKey(FAKE_UID, familyKey);
  return familyKey;
}

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
    const [, , , childId, , subjectId] = path.split('?')[0].split('/');
    if (path.includes('/chapters')) return schema.parse(CHAPTERS[`${childId}/${subjectId}`]);
    return schema.parse(SUBJECTS[childId]);
  });
}
