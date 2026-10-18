import { renderHook, waitFor } from '@testing-library/react-native';

import { api } from '../shared/api';
import { createFamilyKeys, lockText, makeFamilyId } from '../shared/crypto';
import { writeValue } from '../shared/storage';
import { useHome } from './useHome';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const get = api.get as jest.Mock;

const AARYA = '11111111-1111-4111-8111-111111111111';
const ROHAN = '22222222-2222-4222-8222-222222222222';

const SUBJECTS: Record<string, unknown> = {
  [AARYA]: {
    items: [
      { subjectId: 'science', name: 'Science', icon: 'science', chapterCount: 12 },
      { subjectId: 'mathematics', name: 'Mathematics', icon: 'maths', chapterCount: 14 },
    ],
    nextCursor: null,
  },
  [ROHAN]: {
    items: [{ subjectId: 'english', name: 'English', icon: 'english', chapterCount: 4 }],
    nextCursor: null,
  },
};

async function registerFamilyOnThisPhone() {
  const id = makeFamilyId();
  const { familyKey } = await createFamilyKeys(id, 'blue lantern 2026');
  await writeValue('familyKey', familyKey);
  return familyKey;
}

function answerWith(familyKey: string) {
  get.mockImplementation(async (path: string) => {
    if (path === '/v1/me') {
      return {
        children: [
          { childId: AARYA, name: lockText(familyKey, 'Aarya'), classLevel: 6 },
          { childId: ROHAN, name: lockText(familyKey, 'Rohan'), classLevel: 3 },
        ],
      };
    }
    const childId = path.split('/')[3];
    return SUBJECTS[childId];
  });
}

beforeEach(() => get.mockReset());

describe('opening Home', () => {
  it('shows the first Child by name with that Child’s Subjects', async () => {
    answerWith(await registerFamilyOnThisPhone());

    const { result } = await renderHook(() => useHome());

    await waitFor(() => expect(result.current.subjects.status).toBe('ready'));
    expect(result.current.activeChild).toEqual({ id: AARYA, name: 'Aarya', classLevel: 6 });
    expect(result.current.subjects).toEqual({
      status: 'ready',
      items: [
        { subjectId: 'science', name: 'Science', icon: 'science', chapterCount: 12 },
        { subjectId: 'mathematics', name: 'Mathematics', icon: 'maths', chapterCount: 14 },
      ],
    });
    expect(get).toHaveBeenCalledWith(`/v1/children/${AARYA}/subjects`, expect.anything());
  });
});
