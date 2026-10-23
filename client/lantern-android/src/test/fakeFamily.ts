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

export type FakeChapter = {
  chapterId: string;
  number: number;
  title: string;
  summary: string;
  state: 'not-started' | 'planned' | 'taught' | 'checked';
  attemptCount: number;
  conceptsDone: number | null;
  conceptCount: number | null;
};

type Started = [state: FakeChapter['state'], attempts: number, done: number, of: number];

const chaptersOf = (subjectId: string, rows: [string, string, Started?][]): FakeChapter[] =>
  rows.map(([title, summary, started], index) => ({
    chapterId: `${subjectId}-${index + 1}`,
    number: index + 1,
    title,
    summary,
    state: started?.[0] ?? 'not-started',
    attemptCount: started?.[1] ?? 0,
    conceptsDone: started ? started[2] : null,
    conceptCount: started ? started[3] : null,
  }));

export const CHAPTER_ROWS: Record<string, FakeChapter[]> = {
  [`${AARYA}/science`]: chaptersOf('science', [
    [
      'Materials Around Us',
      'The different materials around us and how we choose one for a job.',
      ['checked', 1, 5, 5],
    ],
    [
      'Changes of State',
      'How materials change between solid, liquid and gas.',
      ['planned', 1, 1, 4],
    ],
    ['Measurement of Length and Motion', 'How we measure length and tell whether something moves.'],
  ]),
  [`${AARYA}/mathematics`]: chaptersOf('mathematics', [
    [
      'Patterns in Mathematics',
      'Finding and continuing patterns in numbers and shapes.',
      ['checked', 1, 5, 5],
    ],
    [
      'Lines and Angles',
      'Straight lines, how they meet, and the angle between them.',
      ['taught', 1, 5, 5],
    ],
    [
      'Number Play',
      'Playing with numbers to see how they relate to one another.',
      ['planned', 2, 2, 5],
    ],
    ['Data Handling and Presentation', 'Collecting information and showing it in tables.'],
  ]),
  [`${ROHAN}/english`]: chaptersOf('english', [
    ['Poonam’s Day Out', 'A day spent noticing the animals, plants and people around us.'],
    ['The Plant Fairy', 'How a seed grows into a plant, told as a story.'],
  ]),
};

const PAGE_DEFAULT = 50;

export function chaptersPageOf(all: FakeChapter[], path: string) {
  const params = new URLSearchParams(path.split('?')[1] ?? '');
  const limit = Number(params.get('limit') ?? PAGE_DEFAULT);
  const offset = Number(params.get('cursor') ?? 0);
  const query = (params.get('q') ?? '').toLowerCase();
  const matching = all.filter((chapter) => chapter.title.toLowerCase().includes(query));
  const count = (state: FakeChapter['state']) =>
    all.filter((chapter) => chapter.state === state).length;
  return {
    items: matching.slice(offset, offset + limit),
    nextCursor: offset + limit < matching.length ? String(offset + limit) : null,
    progress: {
      notStarted: count('not-started'),
      planned: count('planned'),
      taught: count('taught'),
      checked: count('checked'),
      states: all.map((chapter) => chapter.state),
    },
  };
}

type FakeCheck = {
  checkId: string;
  checkNumber: number;
  takenAt: string;
  right: number;
  of: number;
  wrong: { conceptId: string; title: string }[];
};
type FakeAttempt = {
  attemptNumber: number;
  plannedAt: string;
  state: FakeChapter['state'];
  conceptsDone: number;
  conceptCount: number;
  checks: FakeCheck[];
};

export const NUMBER_PLAY_ATTEMPTS: FakeAttempt[] = [
  {
    attemptNumber: 2,
    plannedAt: '2026-10-20T10:42:11+00:00',
    state: 'planned',
    conceptsDone: 2,
    conceptCount: 5,
    checks: [],
  },
  {
    attemptNumber: 1,
    plannedAt: '2026-10-01T09:05:40+00:00',
    state: 'checked',
    conceptsDone: 5,
    conceptCount: 5,
    checks: [
      {
        checkId: '7d0e0001-0000-4000-8000-000000000001',
        checkNumber: 1,
        takenAt: '2026-10-04T11:20:03+00:00',
        right: 2,
        of: 5,
        wrong: [
          { conceptId: 'c2', title: 'Place value' },
          { conceptId: 'c4', title: 'Expanded form' },
          { conceptId: 'c5', title: 'Odd and even' },
        ],
      },
    ],
  },
];

const HISTORIES: Record<string, FakeAttempt[]> = { 'mathematics-3': NUMBER_PLAY_ATTEMPTS };

export function historyPageOf(chapterId: string, path: string) {
  const all = HISTORIES[chapterId] ?? [];
  const params = new URLSearchParams(path.split('?')[1] ?? '');
  const limit = Number(params.get('limit') ?? PAGE_DEFAULT);
  const offset = Number(params.get('cursor') ?? 0);
  const [, rows] =
    Object.entries(CHAPTER_ROWS).find(([, chapters]) =>
      chapters.some((chapter) => chapter.chapterId === chapterId),
    ) ?? [];
  const row = rows?.find((chapter) => chapter.chapterId === chapterId);
  return {
    chapter: {
      chapterId,
      number: row?.number ?? 1,
      title: row?.title ?? 'Unknown',
      subjectId: chapterId.split('-')[0],
      subjectName: chapterId.startsWith('mathematics') ? 'Mathematics' : 'Science',
      state: row?.state ?? 'not-started',
      attemptCount: all.length,
      checkCount: all.reduce((sum, attempt) => sum + attempt.checks.length, 0),
      conceptsDone: row?.conceptsDone ?? null,
      conceptCount: row?.conceptCount ?? null,
    },
    items: all.slice(offset, offset + limit),
    nextCursor: offset + limit < all.length ? String(offset + limit) : null,
  };
}

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
    const [, , , childId, kind, name, tail] = path.split('?')[0].split('/');
    if (kind === 'subjects' && tail === 'chapters') {
      return schema.parse(chaptersPageOf(CHAPTER_ROWS[`${childId}/${name}`] ?? [], path));
    }
    if (kind === 'chapters' && tail === 'attempts') return schema.parse(historyPageOf(name, path));
    return schema.parse(SUBJECTS[childId]);
  });
}
