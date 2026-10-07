import type { MarkPart } from '../shared/ui/markParts';

export type IntroPart = MarkPart | 'word';
export type IntroKind = 'full' | 'short' | 'still';
export type IntroStep = { part: IntroPart; delay: number; duration: number };

// The canvas's Opening intro, in milliseconds: the book opens, the mother and child appear, their
// hands reach the page, the light rises to the star, then the wordmark.
const FULL: IntroStep[] = [
  { part: 'book', delay: 100, duration: 450 },
  { part: 'mother', delay: 300, duration: 550 },
  { part: 'child', delay: 550, duration: 550 },
  { part: 'hands', delay: 950, duration: 450 },
  { part: 'light', delay: 1450, duration: 600 },
  { part: 'arch', delay: 1800, duration: 500 },
  { part: 'star', delay: 1950, duration: 500 },
  { part: 'word', delay: 2250, duration: 500 },
];

const SHORT_SCALE = 0.2;

export function introKind({ seen, reduceMotion }: { seen: boolean; reduceMotion: boolean }) {
  if (reduceMotion) return 'still';
  return seen ? 'short' : 'full';
}

export function introTimeline(kind: IntroKind): IntroStep[] {
  const scale = { full: 1, short: SHORT_SCALE, still: 0 }[kind];
  return FULL.map((step) => ({
    part: step.part,
    delay: step.delay * scale,
    duration: step.duration * scale,
  }));
}

export function introLength(kind: IntroKind) {
  return Math.max(...introTimeline(kind).map((step) => step.delay + step.duration));
}
