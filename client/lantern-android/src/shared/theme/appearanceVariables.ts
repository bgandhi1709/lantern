import type { Appearance } from './appearance';
import { isDark } from './appearance';
import { COLOUR_TOKENS, SCALED_TEXT_PX, paletteFor, scaledPx } from './palettes';
import type { ScaledText } from './palettes';

export const VARIABLE_PREFIX = '--app-';

export function appearanceVariables(appearance: Appearance, phoneIsDark: boolean) {
  const palette = paletteFor(appearance.colourScheme, isDark(appearance.theme, phoneIsDark));
  const colours = COLOUR_TOKENS.map((token) => [`${VARIABLE_PREFIX}${token}`, palette[token]]);
  const sizes = (Object.keys(SCALED_TEXT_PX) as ScaledText[]).map((name) => [
    `${VARIABLE_PREFIX}text-${name}`,
    `${scaledPx(name, appearance.textSize)}px`,
  ]);
  return Object.fromEntries([...colours, ...sizes]);
}
