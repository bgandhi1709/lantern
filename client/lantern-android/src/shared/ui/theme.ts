import { Easing } from 'react-native';

// Colours that code needs as values (the SVG parts). Everything else is a class name from global.css,
// which holds the same colours; theme.test.ts keeps the two in step.
export const colors = {
  primary: '#0F766E',
  primaryDeep: '#115E59',
  text: '#1E293B',
  textSecondary: '#55657A',
  ground: '#F2FAF9',
  card: '#FFFFFF',
  border: '#E1EAE8',
  iconTile: '#E6FAF7',
  onPrimary: '#FFFFFF',
  error: '#B42318',
};

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
