import {
  MAX_CHILDREN,
  birthYearChoices,
  classChoices,
  emptyChild,
  isChildComplete,
  isChildTouched,
  isNameValid,
  isSchoolValid,
  blockingExtra,
} from './child';

const complete = { name: 'Aarav', classLevel: 4, birthYear: '2017', school: '' };
const NOW = new Date('2026-10-13T00:00:00Z');

describe('the Class choices', () => {
  it('are 1 to 10 for CBSE and 1 to 5 for SSC', () => {
    expect(classChoices('cbse')).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
    expect(classChoices('ssc')).toEqual([1, 2, 3, 4, 5]);
  });
});

describe('the birth years', () => {
  it('run from this year minus 3 back to this year minus 18, newest first', () => {
    const years = birthYearChoices(NOW);
    expect(years[0]).toBe('2023');
    expect(years.at(-1)).toBe('2008');
    expect(years).toHaveLength(16);
  });

  it('move on with the calendar', () => {
    expect(birthYearChoices(new Date('2027-01-01T00:00:00Z'))[0]).toBe('2024');
  });
});

describe('a Child name', () => {
  it('needs a letter, at most 60 characters and no control characters', () => {
    expect(isNameValid('Aarav')).toBe(true);
    expect(isNameValid('  ')).toBe(false);
    expect(isNameValid('')).toBe(false);
    expect(isNameValid('a'.repeat(61))).toBe(false);
    expect(isNameValid('a'.repeat(60))).toBe(true);
    expect(isNameValid('Aa\nrav')).toBe(false);
  });
});

describe('a School', () => {
  it('may be empty, and is at most 100 characters with no control characters', () => {
    expect(isSchoolValid('')).toBe(true);
    expect(isSchoolValid('St. Mary High School')).toBe(true);
    expect(isSchoolValid('s'.repeat(101))).toBe(false);
    expect(isSchoolValid('S\tchool')).toBe(false);
  });
});

describe('a complete Child', () => {
  it('has a name, a Class on the Board, a birth year in range and a valid School', () => {
    expect(isChildComplete(complete, 'cbse', NOW)).toBe(true);
    expect(isChildComplete({ ...complete, name: ' ' }, 'cbse', NOW)).toBe(false);
    expect(isChildComplete({ ...complete, classLevel: null }, 'cbse', NOW)).toBe(false);
    expect(isChildComplete({ ...complete, birthYear: '' }, 'cbse', NOW)).toBe(false);
    expect(isChildComplete({ ...complete, birthYear: '2005' }, 'cbse', NOW)).toBe(false);
    expect(isChildComplete({ ...complete, school: 's'.repeat(101) }, 'cbse', NOW)).toBe(false);
  });

  it('is not complete when the Class is not on the Board', () => {
    expect(isChildComplete({ ...complete, classLevel: 7 }, 'ssc', NOW)).toBe(false);
  });
});

describe('a touched Child', () => {
  it('has anything typed or picked', () => {
    expect(isChildTouched(emptyChild())).toBe(false);
    expect(isChildTouched({ ...emptyChild(), name: '   ' })).toBe(false);
    expect(isChildTouched({ ...emptyChild(), name: 'A' })).toBe(true);
    expect(isChildTouched({ ...emptyChild(), classLevel: 1 })).toBe(true);
    expect(isChildTouched({ ...emptyChild(), birthYear: '2015' })).toBe(true);
    expect(isChildTouched({ ...emptyChild(), school: 'S' })).toBe(true);
  });
});

describe('the extra Children', () => {
  it('block Finish when one is half-filled, and the blocking one is the first such', () => {
    const half = { ...emptyChild(), name: 'Mira' };
    expect(blockingExtra([emptyChild(), complete], 'cbse', NOW)).toBeNull();
    expect(blockingExtra([complete, half, emptyChild()], 'cbse', NOW)).toBe(1);
  });

  it('are at most 6 Children with the first', () => {
    expect(MAX_CHILDREN).toBe(6);
  });
});
