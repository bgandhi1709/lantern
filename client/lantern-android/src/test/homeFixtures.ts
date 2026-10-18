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
