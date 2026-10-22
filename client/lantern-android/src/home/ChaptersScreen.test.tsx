import '@testing-library/react-native/matchers';
import { render, screen, userEvent } from '@testing-library/react-native';

import { api } from '../shared/api';
import { clearAll, readValue } from '../shared/storage';
import { AARYA, ROHAN, answerLikeTheApi, registerFamilyOnThisPhone } from '../test/fakeFamily';
import { fakeRestoredSession } from '../test/fakeFirebaseAuth';
import { ChaptersScreen } from './ChaptersScreen';

jest.mock('../shared/api', () => jest.requireActual('../test/fakeApiModule'));

const get = api.get as jest.Mock;

const onBack = jest.fn();
const onOpenTab = jest.fn();
const onAddChild = jest.fn();

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

async function answerChaptersWith(answer: (path: string) => Promise<unknown> | undefined) {
  answerLikeTheApi(get, await registerFamilyOnThisPhone());
  const real = get.getMockImplementation();
  get.mockImplementation(
    (path: string, schema: unknown) =>
      (path.includes('/chapters') ? answer(path) : undefined) ?? real?.(path, schema),
  );
}

beforeEach(async () => {
  await clearAll();
  get.mockReset();
  onBack.mockReset();
  onOpenTab.mockReset();
  onAddChild.mockReset();
});

describe('Chapters', () => {
  it('lists the Subject’s Chapters in book order, each with its number and Chapter summary', async () => {
    await openChapters();

    expect(screen.getByText('Science for Aarya, in book order')).toBeOnTheScreen();
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
    expect(screen.getByText('Mathematics for Aarya, in book order')).toBeOnTheScreen();
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

  it('does not make a Chapter a button, because the Plan comes later', async () => {
    await openChapters();

    expect(screen.queryByRole('button', { name: /Materials Around Us/ })).not.toBeOnTheScreen();
  });

  it('switches Child from the sheet, starts on that Child’s first Subject and keeps only the id on the device', async () => {
    const { user } = await openChapters();

    await user.press(screen.getByRole('button', { name: 'Switch child, now Aarya' }));
    await user.press(await screen.findByRole('button', { name: 'Rohan, Class 3' }));

    expect(await screen.findByText('Poonam’s Day Out')).toBeOnTheScreen();
    expect(
      screen.queryByRole('button', { name: /Switch subject, now Science/ }),
    ).not.toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Switch subject, now English' })).toBeOnTheScreen();
    expect(screen.getByText('English for Rohan, in book order')).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Switch child, now Rohan' })).toBeOnTheScreen();
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
    await answerChaptersWith(() =>
      ++calls === 1 ? Promise.reject(new Error('offline')) : undefined,
    );
    const { user } = await showChapters();

    expect(await screen.findByText("Can't load the Chapters")).toBeOnTheScreen();
    await user.press(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('Materials Around Us')).toBeOnTheScreen();
    expect(screen.queryByText("Can't load the Chapters")).not.toBeOnTheScreen();
  });

  it('follows the cursor until every Chapter is in', async () => {
    const page = (n: number, nextCursor: string | null) => ({
      items: [
        { chapterId: `c${n}`, number: n, title: `Chapter ${n} title`, summary: `About ${n}` },
      ],
      nextCursor,
    });
    await answerChaptersWith((path) => {
      if (path.includes('cursor=abc%2B1')) return Promise.resolve(page(2, null));
      return Promise.resolve(page(1, 'abc+1'));
    });

    await showChapters();

    expect(await screen.findByText('Chapter 2 title')).toBeOnTheScreen();
    expect(screen.getByText('Chapter 1 title')).toBeOnTheScreen();
    expect(get).toHaveBeenCalledWith(
      `/v1/children/${AARYA}/subjects/science/chapters?limit=100&cursor=abc%2B1`,
      expect.anything(),
    );
  });
});
