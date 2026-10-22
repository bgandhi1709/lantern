import '@testing-library/react-native/matchers';
import { fireEvent, render, screen, userEvent, waitFor } from '@testing-library/react-native';

import { api } from '../shared/api';
import { clearAll } from '../shared/storage';
import {
  AARYA,
  answerLikeTheApi,
  historyPageOf,
  registerFamilyOnThisPhone,
} from '../test/fakeFamily';
import { fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { ChapterHistoryScreen } from './ChapterHistoryScreen';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const get = api.get as jest.Mock;
const onBack = jest.fn();
const onAction = jest.fn();

async function showHistory(chapterId = 'mathematics-3') {
  fakeRestoredSession();
  const user = userEvent.setup();
  await render(
    <ChapterHistoryScreen
      childId={AARYA}
      chapterId={chapterId}
      onBack={onBack}
      onAction={onAction}
    />,
  );
  return { user };
}

async function openHistory(chapterId = 'mathematics-3') {
  answerLikeTheApi(get, await registerFamilyOnThisPhone());
  const shown = await showHistory(chapterId);
  await screen.findByText('Chapter history');
  return shown;
}

async function answerAttemptsWith(answer: (path: string) => unknown) {
  answerLikeTheApi(get, await registerFamilyOnThisPhone());
  const real = get.getMockImplementation();
  get.mockImplementation((path: string, schema: { parse: (value: unknown) => unknown }) =>
    path.includes('/attempts')
      ? Promise.resolve(answer(path)).then((value) => schema.parse(value))
      : real?.(path, schema),
  );
}

const attemptRequests = () =>
  get.mock.calls
    .map(([path]: [string]) => path)
    .filter((path: string) => path.includes('/attempts'));

beforeEach(async () => {
  await clearAll();
  get.mockReset();
  onBack.mockReset();
  onAction.mockReset();
});

describe('Chapter history', () => {
  it('names the Chapter, where it stands now and how much of it is kept', async () => {
    await openHistory();

    expect(await screen.findByText('Mathematics · Chapter 3')).toBeOnTheScreen();
    expect(screen.getByText('Number Play')).toBeOnTheScreen();
    expect(screen.getByText('Now: Planned · Attempt 2 · 2 of 5 Concepts done')).toBeOnTheScreen();
    expect(screen.getByText('2 attempts · 1 Check · every one is kept')).toBeOnTheScreen();
  });

  it('lists the Attempts newest first, each with its state and when it was planned', async () => {
    await openHistory();

    const titles = (await screen.findAllByText(/^Attempt \d$/)).map((node) => node.props.children);
    expect(titles).toEqual(['Attempt 2', 'Attempt 1']);
    expect(screen.getByText('Planned 20 Oct')).toBeOnTheScreen();
    expect(screen.getByText('Planned 1 Oct')).toBeOnTheScreen();
    expect(screen.getByText('2 of 5 Concepts done')).toBeOnTheScreen();
    expect(screen.getByText('5 of 5 Concepts done')).toBeOnTheScreen();
  });

  it('says so for an Attempt with no Check, and shows each Check with its mark and wrong Concepts', async () => {
    await openHistory();

    expect(await screen.findByText('No Check taken yet.')).toBeOnTheScreen();
    expect(screen.getByText('Check 1')).toBeOnTheScreen();
    expect(screen.getByText('4 Oct')).toBeOnTheScreen();
    expect(screen.getByText('2/5')).toBeOnTheScreen();
    expect(screen.getByText('Wrong: Place value; Expanded form; Odd and even')).toBeOnTheScreen();
  });

  it('offers the next step for the Chapter at the foot', async () => {
    const { user } = await openHistory();

    await user.press(await screen.findByRole('button', { name: 'Continue this Plan' }));

    expect(onAction).toHaveBeenCalledTimes(1);
  });

  it('goes back from the back button', async () => {
    const { user } = await openHistory();

    await user.press(screen.getByRole('button', { name: 'Back' }));

    expect(onBack).toHaveBeenCalledTimes(1);
  });

  it('says there are no Attempts for a Chapter that has none', async () => {
    await openHistory('science-3');

    expect(await screen.findByText('No Attempts yet.')).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Plan this Chapter' })).toBeOnTheScreen();
  });

  it('shows a loading state, then says the history can’t load and loads it on Try again', async () => {
    let calls = 0;
    await answerAttemptsWith((path) =>
      ++calls === 1 ? Promise.reject(new Error('offline')) : historyPageOf('mathematics-3', path),
    );
    const { user } = await showHistory();

    expect(await screen.findByText("Can't load the history")).toBeOnTheScreen();
    await user.press(screen.getByRole('button', { name: 'Try again' }));

    expect(
      await screen.findByText('Now: Planned · Attempt 2 · 2 of 5 Concepts done'),
    ).toBeOnTheScreen();
  });

  it('loads Attempts 10 at a time as the list is scrolled', async () => {
    const many = Array.from({ length: 25 }, (_, index) => ({
      attemptNumber: 25 - index,
      plannedAt: '2026-10-01T09:05:40+00:00',
      state: 'planned' as const,
      conceptsDone: 0,
      conceptCount: 5,
      checks: [],
    }));
    await answerAttemptsWith((path) => {
      const offset = Number(new URLSearchParams(path.split('?')[1]).get('cursor') ?? 0);
      const base = historyPageOf('mathematics-3', path);
      return {
        ...base,
        items: many.slice(offset, offset + 10),
        nextCursor: offset + 10 < 25 ? String(offset + 10) : null,
      };
    });
    await showHistory();
    await screen.findByText('Chapter history');
    await waitFor(() => expect(attemptRequests()).toHaveLength(1));
    expect(attemptRequests()[0]).toBe(
      `/v1/children/${AARYA}/chapters/mathematics-3/attempts?limit=10`,
    );

    await fireEvent(screen.getByTestId('history-list'), 'endReached');

    await waitFor(() => expect(attemptRequests()).toHaveLength(2));
    expect(attemptRequests()[1]).toBe(
      `/v1/children/${AARYA}/chapters/mathematics-3/attempts?limit=10&cursor=10`,
    );
  });
});
