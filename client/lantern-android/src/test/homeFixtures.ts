import type { Chapter } from '../home/chaptersApi';
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
  },
  {
    chapterId: 'science-2',
    number: 2,
    title: 'Changes of State',
    summary: 'How materials change between solid, liquid and gas when they are heated or cooled.',
  },
  {
    chapterId: 'science-3',
    number: 3,
    title: 'Measurement of Length and Motion',
    summary: 'How we measure length, and how we tell whether something is moving.',
  },
];
