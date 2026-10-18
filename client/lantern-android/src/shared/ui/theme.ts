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

// The mother-and-child mark: the figures take the accent; the light is always amber.
export const markColors = {
  motherHair: '#134E4A',
  motherClothes: colors.primary,
  face: '#5EEAD4',
  childHair: colors.primaryDeep,
  childClothes: '#2DD4BF',
  arch: '#99F6E4',
  light: '#F59E0B',
  glow: '#FDE68A',
};

// Strong ease-out for anything entering the screen.
export const motion = { easeOut: Easing.bezier(0.23, 1, 0.32, 1) };
