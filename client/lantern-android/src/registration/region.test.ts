import { REGION_MAX_LENGTH, isValidRegion } from './region';

describe('the Region rule, the same one /v1/register applies', () => {
  it.each(['Pune, Maharashtra', 'A', 'अहमदाबाद, ગુજરાત', 'x'.repeat(REGION_MAX_LENGTH)])(
    'accepts %s',
    (region) => {
      expect(isValidRegion(region)).toBe(true);
    },
  );

  it.each([
    ['nothing', ''],
    ['only spaces', '   '],
    ['more than 60 characters', 'x'.repeat(REGION_MAX_LENGTH + 1)],
    ['a line break', 'Pune\nMaharashtra'],
    ['a tab', 'Pune,\tMaharashtra'],
    ['a bell character', 'Pune\u0007'],
    ['a zero-width space', 'Pune​, Maharashtra'],
    ['a trailing line break', 'Pune\n'],
    ['a trailing tab', 'Pune\t'],
    ['a trailing byte-order mark', 'Pune\uFEFF'],
    ['spaces that push it past 60 characters', `  ${'x'.repeat(REGION_MAX_LENGTH)}  `],
  ])('refuses %s', (_name, region) => {
    expect(isValidRegion(region)).toBe(false);
  });

  it('counts spaces as the server does, since the Region is sent as typed', () => {
    expect(isValidRegion(` ${'x'.repeat(REGION_MAX_LENGTH - 2)} `)).toBe(true);
  });
});
