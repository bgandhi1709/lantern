import { act, renderHook, waitFor } from '@testing-library/react-native';

import { usePagedList } from './usePagedList';
import type { Page } from './usePagedList';

const page = (items: string[], nextCursor: string | null, meta = 'm'): Page<string, string> => ({
  items,
  nextCursor,
  meta,
});

const deferred = <T,>() => {
  let resolve: (value: T) => void = () => undefined;
  let reject: (reason: unknown) => void = () => undefined;
  const promise = new Promise<T>((done, fail) => {
    resolve = done;
    reject = fail;
  });
  return { promise, resolve, reject };
};

describe('a list that loads a page at a time', () => {
  it('loads the first page, then shows it with what the response said about the whole list', async () => {
    const first = deferred<Page<string, string>>();
    const load = jest.fn().mockReturnValue(first.promise);

    const { result } = await renderHook(() => usePagedList({ key: 'k', load }));

    expect(result.current.status).toBe('loading');
    await act(async () => first.resolve(page(['a', 'b'], 'next', 'whole')));
    await waitFor(() => expect(result.current.status).toBe('ready'));
    expect(result.current.items).toEqual(['a', 'b']);
    expect(result.current.meta).toBe('whole');
    expect(load).toHaveBeenCalledWith(null);
  });

  it('adds the next page when asked, and stops once the server has no more', async () => {
    const load = jest
      .fn()
      .mockResolvedValueOnce(page(['a'], 'c1'))
      .mockResolvedValueOnce(page(['b'], null));
    const { result } = await renderHook(() => usePagedList({ key: 'k', load }));
    await waitFor(() => expect(result.current.status).toBe('ready'));

    await act(async () => result.current.loadMore());
    await waitFor(() => expect(result.current.items).toEqual(['a', 'b']));
    await act(async () => result.current.loadMore());

    expect(load).toHaveBeenCalledTimes(2);
    expect(load).toHaveBeenLastCalledWith('c1');
    expect(result.current.nextCursor).toBeNull();
  });

  it('never asks for the same page twice while one is on its way', async () => {
    const second = deferred<Page<string, string>>();
    const load = jest
      .fn()
      .mockResolvedValueOnce(page(['a'], 'c1'))
      .mockReturnValueOnce(second.promise);
    const { result } = await renderHook(() => usePagedList({ key: 'k', load }));
    await waitFor(() => expect(result.current.status).toBe('ready'));

    await act(async () => {
      result.current.loadMore();
      result.current.loadMore();
    });

    expect(load).toHaveBeenCalledTimes(2);
    expect(result.current.loadingMore).toBe(true);
    await act(async () => second.resolve(page(['b'], null)));
    expect(result.current.loadingMore).toBe(false);
  });

  it('shows a failed first page, and loads it on reload', async () => {
    const load = jest
      .fn()
      .mockRejectedValueOnce(new Error('offline'))
      .mockResolvedValueOnce(page(['a'], null));
    const { result } = await renderHook(() => usePagedList({ key: 'k', load }));
    await waitFor(() => expect(result.current.status).toBe('failed'));

    await act(async () => result.current.reload());

    await waitFor(() => expect(result.current.status).toBe('ready'));
    expect(result.current.items).toEqual(['a']);
  });

  it('keeps what is shown when a later page fails, and gets it on retry', async () => {
    const load = jest
      .fn()
      .mockResolvedValueOnce(page(['a'], 'c1'))
      .mockRejectedValueOnce(new Error('offline'))
      .mockResolvedValueOnce(page(['b'], null));
    const { result } = await renderHook(() => usePagedList({ key: 'k', load }));
    await waitFor(() => expect(result.current.status).toBe('ready'));

    await act(async () => result.current.loadMore());
    await waitFor(() => expect(result.current.moreFailed).toBe(true));
    expect(result.current.items).toEqual(['a']);
    await act(async () => result.current.retryMore());

    await waitFor(() => expect(result.current.items).toEqual(['a', 'b']));
    expect(result.current.moreFailed).toBe(false);
  });

  it('starts again from the first page for a new key, and ignores a late reply to the old one', async () => {
    const old = deferred<Page<string, string>>();
    const load = jest.fn((cursor: string | null) =>
      Promise.resolve(page([`fresh-${cursor ?? 'first'}`], null)),
    );
    load.mockReturnValueOnce(old.promise);
    const { result, rerender } = await renderHook(
      ({ key }: { key: string }) => usePagedList({ key, load }),
      {
        initialProps: { key: 'one' },
      },
    );

    await rerender({ key: 'two' });
    await waitFor(() => expect(result.current.items).toEqual(['fresh-first']));
    await act(async () => old.resolve(page(['stale'], null)));

    expect(result.current.items).toEqual(['fresh-first']);
  });

  it('waits until it is enabled before asking for anything', async () => {
    const load = jest.fn().mockResolvedValue(page(['a'], null));
    const { result, rerender } = await renderHook(
      ({ enabled }: { enabled: boolean }) => usePagedList({ key: 'k', enabled, load }),
      { initialProps: { enabled: false } },
    );

    expect(load).not.toHaveBeenCalled();
    await rerender({ enabled: true });

    await waitFor(() => expect(result.current.status).toBe('ready'));
    expect(load).toHaveBeenCalledTimes(1);
  });

  it('keeps showing the old rows while a new search in the same list loads, but never across lists', async () => {
    const second = deferred<Page<string, string>>();
    const load = jest.fn((cursor: string | null) => Promise.resolve(page(['old'], null)));
    const { result, rerender } = await renderHook(
      ({ key, group }: { key: string; group: string }) => usePagedList({ key, group, load }),
      { initialProps: { key: 'maths/', group: 'maths' } },
    );
    await waitFor(() => expect(result.current.items).toEqual(['old']));
    load.mockReturnValueOnce(second.promise);

    await rerender({ key: 'maths/play', group: 'maths' });

    expect(result.current.items).toEqual(['old']);
    expect(result.current.refreshing).toBe(true);
    await act(async () => second.resolve(page(['play'], null)));
    expect(result.current.items).toEqual(['play']);
    expect(result.current.refreshing).toBe(false);
    load.mockReturnValueOnce(new Promise(() => undefined));
    await rerender({ key: 'science/', group: 'science' });
    expect(result.current.items).toEqual([]);
    expect(result.current.status).toBe('loading');
  });

  it('does not ask for a page with the cursor of a page that has just been replaced', async () => {
    const load = jest
      .fn()
      .mockResolvedValueOnce(page(['a'], 'c1'))
      .mockResolvedValueOnce(page(['b'], 'c2'))
      .mockResolvedValueOnce(page(['c'], null));
    const { result } = await renderHook(() => usePagedList({ key: 'k', load }));
    await waitFor(() => expect(result.current.status).toBe('ready'));

    await act(async () => {
      result.current.loadMore();
    });
    await act(async () => {
      result.current.loadMore();
    });

    expect(load.mock.calls.map(([cursor]) => cursor)).toEqual([null, 'c1', 'c2']);
  });
});
