import { act, renderHook, waitFor } from '@testing-library/react-native';

import { ApiError, api } from '../shared/api';
import { clearAll, readValue } from '../shared/storage';
import {
  AARYA,
  AARYA_SUBJECTS,
  ROHAN,
  answerLikeTheApi,
  registerFamilyOnThisPhone,
} from '../test/fakeFamily';
import { useHome } from './useHome';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const get = api.get as jest.Mock;

beforeEach(async () => {
  get.mockReset();
  await clearAll();
});

describe('opening Home', () => {
  it('shows the first Child by name with that Child’s Subjects', async () => {
    answerLikeTheApi(get, await registerFamilyOnThisPhone());

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

  it('keeps loading and asks again when the Child’s content is still being made', async () => {
    jest.useFakeTimers();
    answerLikeTheApi(get, await registerFamilyOnThisPhone());
    const answer = get.getMockImplementation();
    let subjectsCalls = 0;
    get.mockImplementation(async (path: string, schema: unknown) => {
      if (path === `/v1/children/${AARYA}/subjects` && ++subjectsCalls === 1) {
        throw new ApiError('content-not-ready', 409);
      }
      return answer?.(path, schema);
    });

    const { result } = await renderHook(() => useHome());

    await waitFor(() => expect(subjectsCalls).toBe(1));
    expect(result.current.subjects).toEqual({ status: 'loading' });
    await act(() => jest.advanceTimersByTimeAsync(3000));
    await waitFor(() => expect(result.current.subjects.status).toBe('ready'));
    expect(subjectsCalls).toBe(2);
    jest.useRealTimers();
  });

  it('shows a failure when the call fails and loads again on retry', async () => {
    answerLikeTheApi(get, await registerFamilyOnThisPhone());
    const answer = get.getMockImplementation();
    let subjectsCalls = 0;
    get.mockImplementation(async (path: string, schema: unknown) => {
      if (path === `/v1/children/${AARYA}/subjects` && ++subjectsCalls === 1)
        throw new ApiError('network', null);
      return answer?.(path, schema);
    });

    const { result } = await renderHook(() => useHome());

    await waitFor(() => expect(result.current.subjects).toEqual({ status: 'failed' }));
    await act(async () => result.current.retry());
    await waitFor(() => expect(result.current.subjects.status).toBe('ready'));
    expect(subjectsCalls).toBe(2);
  });

  it('asks the Parent to unlock the Family when this phone has no Family key', async () => {
    answerLikeTheApi(get, 'unused');

    const { result } = await renderHook(() => useHome());

    await waitFor(() => expect(result.current.locked).toBe(true));
    expect(get).not.toHaveBeenCalled();
  });

  it('switches Child, remembers only the id, and opens on that Child next time', async () => {
    answerLikeTheApi(get, await registerFamilyOnThisPhone());
    const { result } = await renderHook(() => useHome());
    await waitFor(() => expect(result.current.subjects.status).toBe('ready'));

    await act(async () => result.current.switchTo(ROHAN));

    expect(result.current.activeChild?.name).toBe('Rohan');
    expect(result.current.subjects).toEqual({
      status: 'ready',
      items: [{ subjectId: 'english', name: 'English', icon: 'english', chapterCount: 4 }],
    });
    expect(await readValue('activeChildId')).toBe(ROHAN);
    const next = await renderHook(() => useHome());
    await waitFor(() => expect(next.result.current.activeChild?.name).toBe('Rohan'));
  });

  it('shows a Child still being made as loading while the active Child is already shown', async () => {
    jest.useFakeTimers();
    answerLikeTheApi(get, await registerFamilyOnThisPhone());
    const answer = get.getMockImplementation();
    get.mockImplementation(async (path: string, schema: unknown) => {
      if (path === `/v1/children/${ROHAN}/subjects`) throw new ApiError('content-not-ready', 409);
      return answer?.(path, schema);
    });

    const { result } = await renderHook(() => useHome());

    await waitFor(() => expect(result.current.subjects.status).toBe('ready'));
    expect(result.current.activeChild?.name).toBe('Aarya');
    expect(result.current.children).toEqual([
      { id: AARYA, name: 'Aarya', classLevel: 6, ready: true },
      { id: ROHAN, name: 'Rohan', classLevel: 3, ready: false },
    ]);
    jest.useRealTimers();
  });
});
