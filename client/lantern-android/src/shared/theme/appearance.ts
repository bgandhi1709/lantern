export const THEME_CHOICES = ['system', 'light', 'dark'] as const;
export const TEXT_SIZES = ['default', 'large', 'largest'] as const;
export const COLOUR_SCHEMES = ['teal', 'blue', 'gray', 'golden'] as const;

export type ThemeChoice = (typeof THEME_CHOICES)[number];
export type TextSize = (typeof TEXT_SIZES)[number];
export type ColourScheme = (typeof COLOUR_SCHEMES)[number];

export type Appearance = { theme: ThemeChoice; textSize: TextSize; colourScheme: ColourScheme };

export const DEFAULT_APPEARANCE: Appearance = {
  theme: 'system',
  textSize: 'default',
  colourScheme: 'teal',
};

export const isDark = (theme: ThemeChoice, phoneIsDark: boolean) =>
  theme === 'system' ? phoneIsDark : theme === 'dark';
