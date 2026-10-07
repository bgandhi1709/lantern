import { en, type Strings } from './en';

export type { Strings };

// English only for the pilot (D44). The Parent's Language setting (English, Gujarati or Hindi)
// will pick the dictionary here; screens keep calling useStrings().
export function useStrings(): Strings {
  return en;
}
