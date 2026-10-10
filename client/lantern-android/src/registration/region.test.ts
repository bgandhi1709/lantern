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
  ])('refuses %s', (_name, region) => {
    expect(isValidRegion(region)).toBe(false);
  });

  it('ignores spaces around the text when counting and checking', () => {
    expect(isValidRegion(`  ${'x'.repeat(REGION_MAX_LENGTH)}  `)).toBe(true);
  });
});
