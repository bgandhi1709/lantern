import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';

import { onSessionChange } from '../session';
import { readValue, writeValue } from '../storage';
import { DEFAULT_APPEARANCE } from './appearance';
import type { Appearance, ColourScheme, TextSize, ThemeChoice } from './appearance';
import { LookProvider } from './LookProvider';

const readAppearance = async (): Promise<Appearance> => {
  const [theme, textSize, colourScheme] = await Promise.all([
    readValue('theme'),
    readValue('textSize'),
    readValue('colourScheme'),
  ]);
  return { theme, textSize, colourScheme };
};

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [appearance, setAppearance] = useState<Appearance | null>(null);
  const wasSignedIn = useRef(false);

  useEffect(() => {
    let cancelled = false;
    readAppearance().then((saved) => {
      if (!cancelled) setAppearance((current) => current ?? saved);
    });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(
    () =>
      onSessionChange((signedIn) => {
        if (wasSignedIn.current && !signedIn) setAppearance(DEFAULT_APPEARANCE);
        wasSignedIn.current = signedIn;
      }),
    [],
  );

  const setTheme = useCallback((theme: ThemeChoice) => {
    setAppearance((current) => ({ ...(current ?? DEFAULT_APPEARANCE), theme }));
    writeValue('theme', theme).catch(() => undefined);
  }, []);

  const setTextSize = useCallback((textSize: TextSize) => {
    setAppearance((current) => ({ ...(current ?? DEFAULT_APPEARANCE), textSize }));
    writeValue('textSize', textSize).catch(() => undefined);
  }, []);

  const setColourScheme = useCallback((colourScheme: ColourScheme) => {
    setAppearance((current) => ({ ...(current ?? DEFAULT_APPEARANCE), colourScheme }));
    writeValue('colourScheme', colourScheme).catch(() => undefined);
  }, []);

  const actions = useMemo(
    () => ({ setTheme, setTextSize, setColourScheme }),
    [setTheme, setTextSize, setColourScheme],
  );

  if (appearance === null) return null;
  return (
    <LookProvider appearance={appearance} actions={actions}>
      {children}
    </LookProvider>
  );
}
