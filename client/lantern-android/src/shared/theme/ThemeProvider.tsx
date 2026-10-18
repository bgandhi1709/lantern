import { VariableContextProvider } from 'nativewind';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { useColorScheme } from 'react-native';

import { colorsOf } from '../ui/theme';
import { onSessionChange } from '../session';
import { readValue, writeValue } from '../storage';
import { DEFAULT_APPEARANCE, isDark } from './appearance';
import type { Appearance, ColourScheme, TextSize, ThemeChoice } from './appearance';
import { AppearanceContext } from './appearanceContext';
import type { AppearanceValue } from './appearanceContext';
import { appearanceVariables } from './appearanceVariables';
import { paletteFor } from './palettes';

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
  const phoneIsDark = useColorScheme() === 'dark';
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

  const value = useMemo<AppearanceValue | null>(() => {
    if (appearance === null) return null;
    const dark = isDark(appearance.theme, phoneIsDark);
    return {
      appearance,
      dark,
      colors: colorsOf(paletteFor(appearance.colourScheme, dark)),
      setTheme,
      setTextSize,
      setColourScheme,
    };
  }, [appearance, phoneIsDark, setTheme, setTextSize, setColourScheme]);

  const variables = useMemo(
    () => (appearance ? appearanceVariables(appearance, phoneIsDark) : {}),
    [appearance, phoneIsDark],
  );

  if (value === null) return null;
  return (
    <AppearanceContext.Provider value={value}>
      <VariableContextProvider value={variables}>{children}</VariableContextProvider>
    </AppearanceContext.Provider>
  );
}
