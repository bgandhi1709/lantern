import { useCallback, useEffect, useRef, useState } from 'react';

export type Page<T, M> = { items: T[]; nextCursor: string | null; meta: M };

type Props<T, M> = {
  key: string;
  group?: string;
  load: (cursor: string | null) => Promise<Page<T, M>>;
  enabled?: boolean;
};

type State<T, M> = {
  token: string;
  group: string;
  items: T[];
  nextCursor: string | null;
  meta: M | null;
  status: 'loading' | 'ready' | 'failed';
  refreshing: boolean;
  loadingMore: boolean;
  moreFailed: boolean;
};

const loadingState = <T, M>(token: string, group: string): State<T, M> => ({
  token,
  group,
  items: [],
  nextCursor: null,
  meta: null,
  status: 'loading',
  refreshing: false,
  loadingMore: false,
  moreFailed: false,
});

export function usePagedList<T, M>({ key, group = key, load, enabled = true }: Props<T, M>) {
  const [attempt, setAttempt] = useState(0);
  const token = `${key}#${attempt}`;
  const [state, setState] = useState<State<T, M>>(() => loadingState(token, group));
  const current: State<T, M> =
    state.token === token
      ? state
      : state.group === group && state.status === 'ready'
        ? { ...state, refreshing: true, loadingMore: false, moreFailed: false }
        : loadingState(token, group);

  const loader = useRef(load);
  const tokenNow = useRef(token);
  const generation = useRef(0);
  const busy = useRef(false);
  const ready = useRef(false);
  const cursor = useRef<string | null>(null);

  useEffect(() => {
    loader.current = load;
    tokenNow.current = token;
  });

  useEffect(() => {
    if (!enabled) return;
    const mine = ++generation.current;
    busy.current = false;
    ready.current = false;
    cursor.current = null;
    loader
      .current(null)
      .then((page) => {
        if (generation.current !== mine) return;
        cursor.current = page.nextCursor;
        ready.current = true;
        setState({ ...loadingState(token, group), ...page, status: 'ready' });
      })
      .catch(() => {
        if (generation.current !== mine) return;
        setState({ ...loadingState(token, group), status: 'failed' });
      });
  }, [token, group, enabled]);

  const loadMore = useCallback(() => {
    if (busy.current || !ready.current || cursor.current === null) return;
    busy.current = true;
    const mine = generation.current;
    const asked = cursor.current;
    const startedFor = tokenNow.current;
    const change = (apply: (previous: State<T, M>) => State<T, M>) =>
      setState((previous) => (previous.token === startedFor ? apply(previous) : previous));
    change((previous) => ({ ...previous, loadingMore: true, moreFailed: false }));
    loader
      .current(asked)
      .then((page) => {
        if (generation.current !== mine) return;
        busy.current = false;
        cursor.current = page.nextCursor;
        change((previous) => ({
          ...previous,
          items: [...previous.items, ...page.items],
          nextCursor: page.nextCursor,
          meta: page.meta,
          loadingMore: false,
        }));
      })
      .catch(() => {
        if (generation.current !== mine) return;
        busy.current = false;
        change((previous) => ({ ...previous, loadingMore: false, moreFailed: true }));
      });
  }, []);

  const reload = useCallback(() => setAttempt((n) => n + 1), []);

  return { ...current, loadMore, retryMore: loadMore, reload };
}
