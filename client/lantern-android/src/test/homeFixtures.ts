import type { Attempt, ChapterHeader } from '../home/attemptsApi';
import type { Chapter, SubjectProgress } from '../home/chaptersApi';
import type { Subject } from '../home/homeApi';
import type { SwitcherChild } from '../home/useHome';

export const CLASS_THREE_SUBJECTS: Subject[] = [
  { subjectId: 'english', name: 'English', icon: 'english', chapterCount: 12 },
  { subjectId: 'mathematics', name: 'Mathematics', icon: 'maths', chapterCount: 14 },
  { subjectId: 'the-world-around-us', name: 'The World Around Us', icon: 'evs', chapterCount: 12 },
];

export const CLASS_SIX_SUBJECTS: Subject[] = [
  { subjectId: 'science', name: 'Science', icon: 'science', chapterCount: 12 },
  { subjectId: 'mathematics', name: 'Mathematics', icon: 'maths', chapterCount: 14 },
];

export const TWO_CHILDREN: SwitcherChild[] = [
  { id: 'aarya', name: 'Aarya', classLevel: 6, stillBeingMade: false },
  { id: 'rohan', name: 'Rohan', classLevel: 3, stillBeingMade: false },
];

export const SIX_CHILDREN: SwitcherChild[] = [
  ...TWO_CHILDREN,
  { id: 'mira', name: 'Mira', classLevel: 2, stillBeingMade: false },
  { id: 'dev', name: 'Dev', classLevel: 4, stillBeingMade: false },
  { id: 'isha', name: 'Isha', classLevel: 5, stillBeingMade: false },
  { id: 'kabir', name: 'Kabir', classLevel: 1, stillBeingMade: false },
];

export const LONG_NAME = 'Venkatasubramanian Raghunathan';

export const SCIENCE_CHAPTERS: Chapter[] = [
  {
    chapterId: 'science-1',
    number: 1,
    title: 'Materials Around Us',
    summary: 'The different materials around us and how we choose the right one for a job.',
    state: 'checked',
    attemptCount: 2,
    conceptsDone: 5,
    conceptCount: 5,
  },
  {
    chapterId: 'science-2',
    number: 2,
    title: 'Changes of State',
    summary: 'How materials change between solid, liquid and gas when they are heated or cooled.',
    state: 'planned',
    attemptCount: 1,
    conceptsDone: 1,
    conceptCount: 4,
  },
  {
    chapterId: 'science-3',
    number: 3,
    title: 'Measurement of Length and Motion',
    summary: 'How we measure length, and how we tell whether something is moving.',
    state: 'not-started',
    attemptCount: 0,
    conceptsDone: null,
    conceptCount: null,
  },
];

export const SCIENCE_PROGRESS: SubjectProgress = {
  notStarted: 1,
  planned: 1,
  taught: 0,
  checked: 1,
  states: ['checked', 'planned', 'not-started'],
};

export const FORTY_FOUR_CHAPTERS_PROGRESS: SubjectProgress = {
  notStarted: 38,
  planned: 3,
  taught: 1,
  checked: 2,
  states: [
    'checked',
    'checked',
    'taught',
    'planned',
    'planned',
    'planned',
    ...Array.from({ length: 38 }, () => 'not-started' as const),
  ],
};

export const NOTHING_STARTED_PROGRESS: SubjectProgress = {
  notStarted: 3,
  planned: 0,
  taught: 0,
  checked: 0,
  states: ['not-started', 'not-started', 'not-started'],
};

export const NUMBER_PLAY_HEADER: ChapterHeader = {
  chapterId: 'mathematics-3',
  number: 3,
  title: 'Number Play',
  subjectId: 'mathematics',
  subjectName: 'Mathematics',
  state: 'planned',
  attemptCount: 2,
  checkCount: 2,
  conceptsDone: 2,
  conceptCount: 5,
};

const WRONG_PLACE_VALUE = [{ conceptId: 'c2', title: 'Place value' }];

export const NUMBER_PLAY_ATTEMPTS: Attempt[] = [
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
        checkId: '7d0e0001-0000-4000-8000-000000000002',
        checkNumber: 2,
        takenAt: '2026-10-09T11:20:03+00:00',
        right: 4,
        of: 5,
        wrong: WRONG_PLACE_VALUE,
      },
      {
        checkId: '7d0e0001-0000-4000-8000-000000000001',
        checkNumber: 1,
        takenAt: '2026-10-04T11:20:03+00:00',
        right: 2,
        of: 5,
        wrong: [
          ...WRONG_PLACE_VALUE,
          { conceptId: 'c4', title: 'Expanded form' },
          { conceptId: 'c5', title: 'Odd and even' },
        ],
      },
    ],
  },
];
