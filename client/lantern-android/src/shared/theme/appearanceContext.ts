import { createContext } from 'react';

import { colorsOf } from '../ui/theme';
import { DEFAULT_APPEARANCE } from './appearance';
import type { Appearance, ColourScheme, TextSize, ThemeChoice } from './appearance';
import { paletteFor } from './palettes';

export type AppearanceValue = {
  appearance: Appearance;
  dark: boolean;
  colors: ReturnType<typeof colorsOf>;
  setTheme: (theme: ThemeChoice) => void;
  setTextSize: (textSize: TextSize) => void;
  setColourScheme: (colourScheme: ColourScheme) => void;
};

const nothing = () => undefined;

export const AppearanceContext = createContext<AppearanceValue>({
  appearance: DEFAULT_APPEARANCE,
  dark: false,
  colors: colorsOf(paletteFor('teal', false)),
  setTheme: nothing,
  setTextSize: nothing,
  setColourScheme: nothing,
});
