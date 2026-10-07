import { introKind, introLength, introTimeline } from './intro';

describe('introKind', () => {
  it('plays the full intro on a new install', () => {
    expect(introKind({ seen: false, reduceMotion: false })).toBe('full');
  });

  it('plays the short intro once the intro has been seen', () => {
    expect(introKind({ seen: true, reduceMotion: false })).toBe('short');
  });

  it('shows the still mark when the phone asks for less motion', () => {
    expect(introKind({ seen: false, reduceMotion: true })).toBe('still');
  });
});

describe('introTimeline', () => {
  it('reveals the book, mother, child, hands, light, arch, star and word in that order', () => {
    const parts = [...introTimeline('full')].sort((a, b) => a.delay - b.delay).map((s) => s.part);

    expect(parts).toEqual(['book', 'mother', 'child', 'hands', 'light', 'arch', 'star', 'word']);
  });

  it('lasts under three seconds in full, about a second short, and nothing still', () => {
    expect(introLength('full')).toBeGreaterThan(2000);
    expect(introLength('full')).toBeLessThan(3000);
    expect(introLength('short')).toBeGreaterThanOrEqual(900);
    expect(introLength('short')).toBeLessThanOrEqual(1100);
    expect(introLength('still')).toBe(0);
  });
});
