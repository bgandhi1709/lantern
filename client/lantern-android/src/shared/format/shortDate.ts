const LOCALE = 'en-IN';

export function shortDate(iso: string) {
  return new Date(iso).toLocaleDateString(LOCALE, { day: 'numeric', month: 'short' });
}
