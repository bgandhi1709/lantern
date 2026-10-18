import { z } from 'zod';

import { COLOUR_SCHEMES, TEXT_SIZES, THEME_CHOICES } from '../theme/appearance';
import type { ColourScheme, TextSize, ThemeChoice } from '../theme/appearance';

const STORAGE_VERSION = 1;

export const KEY_PREFIX = 'lantern.';

type Values = {
  welcomeDismissed: boolean;
  introSeen: boolean;
  activeChildId: string | null;
  familyKey: string | null;
  theme: ThemeChoice;
  textSize: TextSize;
  colourScheme: ColourScheme;
};

type Definition<T> = { schema: z.ZodType<T>; fallback: T; secure: boolean };

export type StoredName = keyof Values;
export type StoredValue<N extends StoredName> = Values[N];

export const storedValues: { [N in StoredName]: Definition<Values[N]> } = {
  welcomeDismissed: { schema: z.boolean(), fallback: false, secure: false },
  introSeen: { schema: z.boolean(), fallback: false, secure: false },
  activeChildId: { schema: z.string().nullable(), fallback: null, secure: false },
  familyKey: { schema: z.string().nullable(), fallback: null, secure: true },
  theme: { schema: z.enum(THEME_CHOICES), fallback: 'system', secure: false },
  textSize: { schema: z.enum(TEXT_SIZES), fallback: 'default', secure: false },
  colourScheme: { schema: z.enum(COLOUR_SCHEMES), fallback: 'teal', secure: false },
};

export const storageKey = (name: StoredName) => `${KEY_PREFIX}v${STORAGE_VERSION}.${name}`;
