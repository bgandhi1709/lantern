import '@testing-library/react-native/matchers';
import { fireEvent, render, screen, userEvent, waitFor } from '@testing-library/react-native';

import { api } from '../shared/api';
import { clearAll, readValue } from '../shared/storage';
import {
  AARYA,
  ROHAN,
  answerLikeTheApi,
  chaptersPageOf,
  registerFamilyOnThisPhone,
} from '../test/fakeFamily';
import type { FakeChapter } from '../test/fakeFamily';
import { fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { ChaptersScreen } from './ChaptersScreen';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const get = api.get as jest.Mock;

const onBack = jest.fn();
const onOpenTab = jest.fn();
const onAddChild = jest.fn();
const onAction = jest.fn();
const onOpenHistory = jest.fn();

async function showChapters(childId = AARYA, subjectId = 'science') {
  fakeRestoredSession();
  const user = userEvent.setup();
  await render(
    <ChaptersScreen
      childId={childId}
      subjectId={subjectId}
      onBack={onBack}
      onOpenTab={onOpenTab}
      onAddChild={onAddChild}
      onAction={onAction}
      onOpenHistory={onOpenHistory}
    />,
  );
  return { user };
}

async function openChapters(childId = AARYA, subjectId = 'science') {
  answerLikeTheApi(get, await registerFamilyOnThisPhone());
  const shown = await showChapters(childId, subjectId);
  await screen.findByText(
    subjectId === 'science' ? 'Materials Around Us' : 'Patterns in Mathematics',
  );
  return shown;
}

async function answerChaptersWith(answer: (path: string) => unknown) {
  answerLikeTheApi(get, await registerFamilyOnThisPhone());
  const real = get.getMockImplementation();
  get.mockImplementation((path: string, schema: { parse: (value: unknown) => unknown }) =>
    path.includes('/chapters?') && !path.includes('/attempts')
      ? Promise.resolve(answer(path)).then((value) => schema.parse(value))
      : real?.(path, schema),
  );
}

const longList = (count: number): FakeChapter[] =>
  Array.from({ length: count }, (_, index) => ({
    chapterId: `evs-${index + 1}`,
    number: index + 1,
    title: `Environment topic ${index + 1}`,
    summary: 'About our surroundings.',
    state: 'not-started',
    attemptCount: 0,
    conceptsDone: null,
    conceptCount: null,
  }));

const chapterRequests = () =>
  get.mock.calls
    .map(([path]: [string]) => path)
    .filter((path: string) => path.includes('/chapters?'));

beforeEach(async () => {
  await clearAll();
  get.mockReset();
  [onBack, onOpenTab, onAddChild, onAction, onOpenHistory].forEach((fn) => fn.mockReset());
});

describe('Chapters', () => {
  it('lists the Chapters in book order, each with its number, title and Chapter summary', async () => {
    await openChapters();

    const titles = screen
      .getAllByText(/^(Materials|Changes|Measurement)/)
      .map((node) => node.props.children);
    expect(titles).toEqual([
      'Materials Around Us',
      'Changes of State',
      'Measurement of Length and Motion',
    ]);
    const numbers = screen.getAllByText(/^[1-3]$/, { includeHiddenElements: true });
    expect(numbers.map((node) => node.props.children)).toEqual([1, 2, 3]);
    expect(
      screen.getByText('How materials change between solid, liquid and gas.'),
    ).toBeOnTheScreen();
  });

  it('says where each Chapter stands and offers the one action for that state', async () => {
    await openChapters(AARYA, 'mathematics');

    expect(
      screen.getByRole('button', { name: 'Patterns in Mathematics, Checked. Plan again' }),
    ).toBeOnTheScreen();
    expect(
      screen.getByRole('button', { name: 'Lines and Angles, Taught. Check' }),
    ).toBeOnTheScreen();
    expect(
      screen.getByRole('button', {
        name: 'Number Play, Planned · Attempt 2 · 2 of 5 Concepts done. Continue',
      }),
    ).toBeOnTheScreen();
    expect(
      screen.getByRole('button', { name: 'Data Handling and Presentation, Not started. Plan' }),
    ).toBeOnTheScreen();
  });

  it('opens the stub for a Chapter’s action', async () => {
    const { user } = await openChapters(AARYA, 'mathematics');

    await user.press(screen.getByRole('button', { name: /^Lines and Angles, Taught/ }));

    expect(onAction).toHaveBeenCalledTimes(1);
  });

  it('counts the Subject’s Chapters by state in a card at the top', async () => {
    await openChapters(AARYA, 'mathematics');

    expect(screen.getByText('Mathematics for Aarya')).toBeOnTheScreen();
    expect(screen.getByText('1 of 4 checked')).toBeOnTheScreen();
    expect(screen.getByText('1 taught')).toBeOnTheScreen();
    expect(screen.getByText('1 planned')).toBeOnTheScreen();
    expect(screen.getByText('1 not started')).toBeOnTheScreen();
  });

  it('shows how many Attempts a Chapter has, only when it has some, and opens its history', async () => {
    const { user } = await openChapters(AARYA, 'mathematics');

    expect(screen.getAllByText('View history')).toHaveLength(3);
    expect(screen.getByText('2 attempts')).toBeOnTheScreen();
    await user.press(
      screen.getByRole('button', { name: 'View history of Number Play, 2 attempts' }),
    );

    expect(onOpenHistory).toHaveBeenCalledWith(AARYA, 'mathematics-3');
  });

  it('names the opened Subject on a drop-down button', async () => {
    await openChapters();

    expect(screen.getByRole('button', { name: 'Switch subject, now Science' })).toBeOnTheScreen();
  });

  it('lists the Child’s Subjects in the drop-down with the opened one selected', async () => {
    const { user } = await openChapters();

    await user.press(screen.getByRole('button', { name: 'Switch subject, now Science' }));

    expect(await screen.findByText('Pick a Subject')).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Science, 12 Chapters' })).toBeSelected();
    expect(screen.getByRole('button', { name: 'Mathematics, 14 Chapters' })).not.toBeSelected();
  });

  it('switches Subject from the drop-down and closes it', async () => {
    const { user } = await openChapters();

    await user.press(screen.getByRole('button', { name: 'Switch subject, now Science' }));
    await user.press(await screen.findByRole('button', { name: 'Mathematics, 14 Chapters' }));

    expect(await screen.findByText('Lines and Angles')).toBeOnTheScreen();
    expect(screen.queryByText('Materials Around Us')).not.toBeOnTheScreen();
    expect(screen.queryByText('Pick a Subject')).not.toBeOnTheScreen();
    expect(
      screen.getByRole('button', { name: 'Switch subject, now Mathematics' }),
    ).toBeOnTheScreen();
  });

  it('closes the drop-down without changing the Subject', async () => {
    const { user } = await openChapters();

    await user.press(screen.getByRole('button', { name: 'Switch subject, now Science' }));
    await user.press(await screen.findByRole('button', { name: 'Close' }));

    expect(screen.queryByText('Pick a Subject')).not.toBeOnTheScreen();
    expect(screen.getByText('Materials Around Us')).toBeOnTheScreen();
  });

  it('opens on the Subject it was given', async () => {
    await openChapters(AARYA, 'mathematics');

    expect(
      screen.getByRole('button', { name: 'Switch subject, now Mathematics' }),
    ).toBeOnTheScreen();
    expect(screen.queryByText('Materials Around Us')).not.toBeOnTheScreen();
  });

  it('goes back from the back button', async () => {
    const { user } = await openChapters();

    await user.press(screen.getByRole('button', { name: 'Back' }));

    expect(onBack).toHaveBeenCalledTimes(1);
  });

  it('switches Child from the sheet, starts on that Child’s first Subject and keeps only the id on the device', async () => {
    const { user } = await openChapters();

    await user.press(screen.getByRole('button', { name: 'Switch child, now Aarya' }));
    await user.press(await screen.findByRole('button', { name: 'Rohan, Class 3' }));

    expect(await screen.findByText('Poonam’s Day Out')).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Switch subject, now English' })).toBeOnTheScreen();
    expect(screen.getByText('English for Rohan')).toBeOnTheScreen();
    expect(await readValue('activeChildId')).toBe(ROHAN);
  });

  it('offers Add a child in the sheet', async () => {
    const { user } = await openChapters();

    await user.press(screen.getByRole('button', { name: 'Switch child, now Aarya' }));
    await user.press(await screen.findByRole('button', { name: 'Add a child' }));

    expect(onAddChild).toHaveBeenCalledTimes(1);
  });

  it('opens a tab other than Home as coming next', async () => {
    const { user } = await openChapters();

    await user.press(screen.getByRole('tab', { name: 'Teach' }));

    expect(onOpenTab).toHaveBeenCalledTimes(1);
  });

  it('shows a loading state until the Chapters arrive', async () => {
    await answerChaptersWith(() => new Promise(() => undefined));

    await showChapters();

    expect(await screen.findByText('Getting the Chapters ready…')).toBeOnTheScreen();
    expect(
      await screen.findByRole('button', { name: 'Switch subject, now Science' }),
    ).toBeOnTheScreen();
  });

  it('says the Chapters can’t load and loads them on Try again', async () => {
    let calls = 0;
    await answerChaptersWith((path) =>
      ++calls === 1 ? Promise.reject(new Error('offline')) : chaptersPageOf(longList(2), path),
    );
    const { user } = await showChapters();

    expect(await screen.findByText("Can't load the Chapters")).toBeOnTheScreen();
    await user.press(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('Environment topic 1')).toBeOnTheScreen();
    expect(screen.queryByText("Can't load the Chapters")).not.toBeOnTheScreen();
  });
});

describe('Chapters, a long Subject', () => {
  it('asks for 20 at a time, and the next 20 when the list reaches its end', async () => {
    await answerChaptersWith((path) => chaptersPageOf(longList(45), path));
    await showChapters();
    await screen.findByText('Environment topic 1');
    expect(chapterRequests()).toEqual([`/v1/children/${AARYA}/subjects/science/chapters?limit=20`]);

    await fireEvent(screen.getByTestId('chapters-list'), 'endReached');

    await waitFor(() => expect(chapterRequests()).toHaveLength(2));
    expect(chapterRequests()[1]).toBe(
      `/v1/children/${AARYA}/subjects/science/chapters?limit=20&cursor=20`,
    );
  });

  it('asks for each page once, in order, and nothing past the last', async () => {
    await answerChaptersWith((path) => chaptersPageOf(longList(45), path));
    await showChapters();
    await screen.findByText('Environment topic 1');
    const list = screen.getByTestId('chapters-list');

    for (let reached = 0; reached < 6; reached++) {
      await fireEvent(list, 'endReached');
    }

    await waitFor(() => expect(chapterRequests()).toHaveLength(3));
    expect(chapterRequests().map((path: string) => path.split('cursor=')[1])).toEqual([
      undefined,
      '20',
      '40',
    ]);
  });

  it('keeps the Chapters shown when a page fails, and loads it on Try again', async () => {
    let failedOnce = false;
    await answerChaptersWith((path) => {
      if (path.includes('cursor=20') && !failedOnce) {
        failedOnce = true;
        return Promise.reject(new Error('offline'));
      }
      return chaptersPageOf(longList(45), path);
    });
    const { user } = await showChapters();
    await screen.findByText('Environment topic 1');

    await fireEvent(screen.getByTestId('chapters-list'), 'endReached');
    expect(await screen.findByText("Can't load more Chapters")).toBeOnTheScreen();
    expect(screen.getByText('Environment topic 1')).toBeOnTheScreen();
    await user.press(screen.getByRole('button', { name: 'Try again' }));

    await waitFor(() =>
      expect(screen.queryByText("Can't load more Chapters")).not.toBeOnTheScreen(),
    );
    expect(chapterRequests().filter((path: string) => path.includes('cursor=20'))).toHaveLength(2);
  });
});

describe('Chapters, finding one', () => {
  it('asks the server for the Chapters whose name contains what was typed', async () => {
    const { user } = await openChapters();

    await user.type(screen.getByLabelText('Find a Chapter'), 'state');

    await waitFor(() =>
      expect(chapterRequests().at(-1)).toBe(
        `/v1/children/${AARYA}/subjects/science/chapters?limit=20&q=state`,
      ),
    );
    await waitFor(() => expect(screen.queryByText('Materials Around Us')).not.toBeOnTheScreen());
    expect(screen.getByText('Changes of State')).toBeOnTheScreen();
  });

  it('keeps the Chapter’s place in the Subject, and the top card still counts the whole Subject', async () => {
    const { user } = await openChapters(AARYA, 'mathematics');

    await user.type(screen.getByLabelText('Find a Chapter'), 'number');

    await waitFor(() => expect(screen.queryByText('Lines and Angles')).not.toBeOnTheScreen());
    expect(screen.getByText('Number Play')).toBeOnTheScreen();
    expect(screen.getByText('3', { includeHiddenElements: true })).toBeOnTheScreen();
    expect(screen.getByText('1 of 4 checked')).toBeOnTheScreen();
  });

  it('says so when nothing matches, and shows everything again once cleared', async () => {
    const { user } = await openChapters();
    const search = screen.getByLabelText('Find a Chapter');

    await user.type(search, 'zebra');
    expect(
      await screen.findByText('No Chapter in Science matches that. Try part of its name.'),
    ).toBeOnTheScreen();
    await user.clear(search);

    expect(await screen.findByText('Materials Around Us')).toBeOnTheScreen();
  });

  it('starts the search again on a Subject switch', async () => {
    const { user } = await openChapters();
    await user.type(screen.getByLabelText('Find a Chapter'), 'state');
    await waitFor(() => expect(screen.queryByText('Materials Around Us')).not.toBeOnTheScreen());

    await user.press(screen.getByRole('button', { name: 'Switch subject, now Science' }));
    await user.press(await screen.findByRole('button', { name: 'Mathematics, 14 Chapters' }));

    expect(await screen.findByText('Patterns in Mathematics')).toBeOnTheScreen();
    expect(screen.getByLabelText('Find a Chapter')).toHaveDisplayValue('');
  });
});
