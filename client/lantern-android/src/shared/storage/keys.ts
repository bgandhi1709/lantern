import { z } from 'zod';

const STORAGE_VERSION = 1;

export const KEY_PREFIX = 'lantern.';

type Values = {
  welcomeDismissed: boolean;
  activeChildId: string | null;
  familyKey: string | null;
};

type Definition<T> = { schema: z.ZodType<T>; fallback: T; secure: boolean };

export type StoredName = keyof Values;
export type StoredValue<N extends StoredName> = Values[N];

export const storedValues: { [N in StoredName]: Definition<Values[N]> } = {
  welcomeDismissed: { schema: z.boolean(), fallback: false, secure: false },
  activeChildId: { schema: z.string().nullable(), fallback: null, secure: false },
  familyKey: { schema: z.string().nullable(), fallback: null, secure: true },
};

export const storageKey = (name: StoredName) => `${KEY_PREFIX}v${STORAGE_VERSION}.${name}`;
