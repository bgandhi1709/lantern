import { shortDate } from './shortDate';

describe('shortDate', () => {
  it('writes the day and the short month', () => {
    expect(shortDate('2026-10-04T11:20:03+00:00')).toBe('4 Oct');
    expect(shortDate('2026-01-31T09:00:00Z')).toBe('31 Jan');
  });
});
