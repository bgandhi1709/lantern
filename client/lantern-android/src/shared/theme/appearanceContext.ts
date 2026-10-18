import { createContext } from 'react';

import { colorsOf } from '../ui/theme';
import { DEFAULT_APPEARANCE } from './appearance';
import type { Appearance, ColourScheme, TextSize, ThemeChoice } from './appearance';
import { markColorsFor, paletteFor } from './palettes';
import type { MarkColors } from './palettes';

export type AppearanceValue = {
  appearance: Appearance;
  dark: boolean;
  colors: ReturnType<typeof colorsOf>;
  markColors: MarkColors;
  setTheme: (theme: ThemeChoice) => void;
  setTextSize: (textSize: TextSize) => void;
  setColourScheme: (colourScheme: ColourScheme) => void;
};

const nothing = () => undefined;

export const AppearanceContext = createContext<AppearanceValue>({
  appearance: DEFAULT_APPEARANCE,
  dark: false,
  colors: colorsOf(paletteFor('teal', false)),
  markColors: markColorsFor('teal', false),
  setTheme: nothing,
  setTextSize: nothing,
  setColourScheme: nothing,
});
