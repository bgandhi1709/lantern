export type DayPart = 'morning' | 'afternoon' | 'evening';

const NOON_HOUR = 12;
const EVENING_HOUR = 17;

export function dayPartAt(hour: number): DayPart {
  if (hour < NOON_HOUR) return 'morning';
  return hour < EVENING_HOUR ? 'afternoon' : 'evening';
}

export const firstNameOf = (fullName: string) => fullName.trim().split(/\s+/)[0] ?? '';
