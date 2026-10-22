import type { Strings } from '../shared/i18n';
import type { Chapter } from './chaptersApi';

type ChaptersText = Strings['home']['chapters'];
type StateFacts = Pick<Chapter, 'state' | 'attemptCount' | 'conceptsDone' | 'conceptCount'>;

export function stateLine(text: ChaptersText, chapter: StateFacts) {
  const attempt = chapter.attemptCount > 1 ? text.attemptNumber(chapter.attemptCount) : null;
  const done =
    chapter.state === 'planned' && chapter.conceptsDone !== null && chapter.conceptCount !== null
      ? text.conceptsDone(chapter.conceptsDone, chapter.conceptCount)
      : null;
  return [text.states[chapter.state], attempt, done].filter(Boolean).join(' · ');
}
