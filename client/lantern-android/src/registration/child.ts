import type { Board } from './board';

// A Family holds at most this many Children, the first included (the API refuses a seventh).
export const MAX_CHILDREN = 6;

export const NAME_MAX_LENGTH = 60;
export const SCHOOL_MAX_LENGTH = 100;

const OLDEST_AGE = 18;
const YOUNGEST_AGE = 3;
const CLASS_COUNT: Record<Board, number> = { cbse: 10, ssc: 5 };

// What the Parent has entered for one Child. The Class is a number, the rest is text.
export type ChildDraft = {
  name: string;
  classLevel: number | null;
  birthYear: string;
  school: string;
};

export const emptyChild = (): ChildDraft => ({
  name: '',
  classLevel: null,
  birthYear: '',
  school: '',
});

export const classChoices = (board: Board) =>
  Array.from({ length: CLASS_COUNT[board] }, (_, index) => index + 1);

// Newest first, so the likeliest years are at the top of the list.
export function birthYearChoices(now: Date) {
  const thisYear = now.getFullYear();
  return Array.from({ length: OLDEST_AGE - YOUNGEST_AGE + 1 }, (_, index) =>
    String(thisYear - YOUNGEST_AGE - index),
  );
}

// The API cannot read a Name or a School (they arrive locked), so the phone applies the rules
// before locking: no control characters, and a length that still fits once locked.
const hasControlCharacters = (text: string) => /\p{C}/u.test(text);

export const isNameValid = (name: string) =>
  name.trim().length > 0 && name.length <= NAME_MAX_LENGTH && !hasControlCharacters(name);

export const isSchoolValid = (school: string) =>
  school.length <= SCHOOL_MAX_LENGTH && !hasControlCharacters(school);

export const isChildTouched = (child: ChildDraft) =>
  child.name.trim() !== '' ||
  child.classLevel !== null ||
  child.birthYear !== '' ||
  child.school.trim() !== '';

export const isChildComplete = (child: ChildDraft, board: Board, now: Date) =>
  isNameValid(child.name) &&
  child.classLevel !== null &&
  classChoices(board).includes(child.classLevel) &&
  birthYearChoices(now).includes(child.birthYear) &&
  isSchoolValid(child.school);

// The index (among the extras) of the first one the Parent started and did not finish, or null.
export function blockingExtra(extras: ChildDraft[], board: Board, now: Date) {
  const index = extras.findIndex(
    (child) => isChildTouched(child) && !isChildComplete(child, board, now),
  );
  return index === -1 ? null : index;
}
