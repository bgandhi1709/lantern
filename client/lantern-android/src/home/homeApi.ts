import { z } from 'zod';

import { api } from '../shared/api';

const FamilyChildren = z.object({
  children: z.array(
    z.object({ childId: z.string(), name: z.string(), classLevel: z.number().int() }),
  ),
});

const SUBJECT_ICONS = ['english', 'maths', 'science', 'evs', 'book'] as const;
const FALLBACK_ICON = 'book';
export type SubjectIcon = (typeof SUBJECT_ICONS)[number];

const SubjectsPage = z.object({
  items: z.array(
    z.object({
      subjectId: z.string(),
      name: z.string(),
      icon: z.enum(SUBJECT_ICONS).catch(FALLBACK_ICON),
      chapterCount: z.number().int(),
    }),
  ),
});

export type Subject = z.infer<typeof SubjectsPage>['items'][number];

export const fetchChildren = async () => (await api.get('/v1/me', FamilyChildren)).children;

export const fetchSubjects = async (childId: string) =>
  (await api.get(`/v1/children/${childId}/subjects`, SubjectsPage)).items;
