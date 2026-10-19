import { Easing } from 'react-native';

import { paletteFor } from '../theme/palettes';
import type { Palette } from '../theme/palettes';

export const colorsOf = (palette: Palette) => ({
  primary: palette.primary,
  accent: palette.accent,
  primaryDeep: palette['primary-deep'],
  text: palette.ink,
  textSecondary: palette.muted,
  ground: palette.ground,
  card: palette.card,
  border: palette.line,
  iconTile: palette.tile,
  onPrimary: palette['on-primary'],
  error: palette.danger,
});

export const colors = colorsOf(paletteFor('teal', false));

// Strong ease-out for anything entering the screen.
export const motion = { easeOut: Easing.bezier(0.23, 1, 0.32, 1) };
