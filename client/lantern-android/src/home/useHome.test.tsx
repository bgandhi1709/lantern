import { act, renderHook, waitFor } from '@testing-library/react-native';
import type { z } from 'zod';

import { ApiError, api } from '../shared/api';
import { clearAll, readValue } from '../shared/storage';
import { AARYA, ROHAN, answerLikeTheApi, registerFamilyOnThisPhone } from '../test/fakeFamily';
import { MAX_CONTENT_POLLS, CONTENT_POLL_MS } from './subjectsOnceReady';
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
      { id: AARYA, name: 'Aarya', classLevel: 6, stillBeingMade: false },
      { id: ROHAN, name: 'Rohan', classLevel: 3, stillBeingMade: true },
    ]);
    jest.useRealTimers();
  });

  it('still lists a Subject whose icon this app does not know yet', async () => {
    answerLikeTheApi(get, await registerFamilyOnThisPhone());
    const answer = get.getMockImplementation();
    get.mockImplementation(async (path: string, schema: z.ZodType) => {
      if (path !== `/v1/children/${AARYA}/subjects`) return answer?.(path, schema);
      return schema.parse({
        items: [
          { subjectId: 'social-science', name: 'Social Science', icon: 'book', chapterCount: 20 },
          { subjectId: 'art', name: 'Art', icon: 'brush-from-the-future', chapterCount: 5 },
        ],
        nextCursor: null,
      });
    });

    const { result } = await renderHook(() => useHome());

    await waitFor(() => expect(result.current.subjects.status).toBe('ready'));
    expect(
      result.current.subjects.status === 'ready' &&
        result.current.subjects.items.map((s) => s.icon),
    ).toEqual(['book', 'book']);
  });
});

describe('when a Child’s call fails', () => {
  it('stops waiting for content that never arrives and shows a failure', async () => {
    jest.useFakeTimers();
    answerLikeTheApi(get, await registerFamilyOnThisPhone());
    const answer = get.getMockImplementation();
    get.mockImplementation(async (path: string, schema: unknown) => {
      if (path === `/v1/children/${AARYA}/subjects`) throw new ApiError('content-not-ready', 409);
      return answer?.(path, schema);
    });

    const { result } = await renderHook(() => useHome());
    await act(() => jest.advanceTimersByTimeAsync(CONTENT_POLL_MS * (MAX_CONTENT_POLLS + 1)));

    await waitFor(() => expect(result.current.subjects).toEqual({ status: 'failed' }));
    jest.useRealTimers();
  });

  it('retries only the Child that failed, without asking for the Children again', async () => {
    answerLikeTheApi(get, await registerFamilyOnThisPhone());
    const answer = get.getMockImplementation();
    let failOnce = true;
    get.mockImplementation(async (path: string, schema: unknown) => {
      if (path === `/v1/children/${AARYA}/subjects` && failOnce) {
        failOnce = false;
        throw new ApiError('network', null);
      }
      return answer?.(path, schema);
    });
    const { result } = await renderHook(() => useHome());
    await waitFor(() => expect(result.current.subjects).toEqual({ status: 'failed' }));
    get.mockClear();

    await act(async () => result.current.retry());

    await waitFor(() => expect(result.current.subjects.status).toBe('ready'));
    expect(get.mock.calls.map(([path]) => path)).toEqual([`/v1/children/${AARYA}/subjects`]);
  });

  it('does not call a sibling whose Subjects failed one that is still being made', async () => {
    answerLikeTheApi(get, await registerFamilyOnThisPhone());
    const answer = get.getMockImplementation();
    get.mockImplementation(async (path: string, schema: unknown) => {
      if (path === `/v1/children/${ROHAN}/subjects`) throw new ApiError('network', null);
      return answer?.(path, schema);
    });

    const { result } = await renderHook(() => useHome());

    await waitFor(() =>
      expect(result.current.children.map((child) => child.stillBeingMade)).toEqual([false, false]),
    );
  });
});
