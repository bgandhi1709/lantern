import type { ColourScheme, TextSize } from './appearance';

export const COLOUR_TOKENS = [
  'primary',
  'primary-deep',
  'accent',
  'ground',
  'card',
  'line',
  'tile',
  'ink',
  'muted',
  'track',
  'on-primary',
  'danger',
  'warning',
  'success',
  'field',
  'disabled',
  'disabled-line',
  'disabled-text',
] as const;

export type ColourToken = (typeof COLOUR_TOKENS)[number];
export type Palette = Record<ColourToken, string>;

type Scheme = {
  accent: string;
  deep: string;
  deepOnDark: string;
  tint: string;
  tintOnDark: string;
  ground: string;
};

const SCHEMES: Record<ColourScheme, Scheme> = {
  teal: {
    accent: '#0f766e',
    deep: '#115e59',
    deepOnDark: '#5eead4',
    tint: '#e6faf7',
    tintOnDark: '#153b37',
    ground: '#f2faf9',
  },
  blue: {
    accent: '#2563eb',
    deep: '#1d4ed8',
    deepOnDark: '#93c5fd',
    tint: '#eaf1ff',
    tintOnDark: '#172a4d',
    ground: '#f4f8ff',
  },
  gray: {
    accent: '#475569',
    deep: '#334155',
    deepOnDark: '#cbd5e1',
    tint: '#eef1f5',
    tintOnDark: '#263241',
    ground: '#f6f7f9',
  },
  golden: {
    accent: '#a16207',
    deep: '#854d0e',
    deepOnDark: '#fcd34d',
    tint: '#fef6dc',
    tintOnDark: '#3a2e10',
    ground: '#fffbef',
  },
};

const LIGHT_BASE = {
  card: '#ffffff',
  line: '#e1eae8',
  ink: '#1e293b',
  muted: '#55657a',
  track: '#d3ddee',
  danger: '#b42318',
  warning: '#8a5300',
  success: '#15803d',
  field: '#7f8b9c',
  disabled: '#e8ecf1',
  'disabled-line': '#a9b4c2',
  'disabled-text': '#6b7686',
};

const DARK_BASE = {
  ground: '#0e1716',
  card: '#16211f',
  line: '#263533',
  ink: '#e5ecea',
  muted: '#a3b3b1',
  track: '#1e2b29',
  danger: '#f87171',
  warning: '#fbbf24',
  success: '#4ade80',
  field: '#7f8b9c',
  disabled: '#1e2b29',
  'disabled-line': '#3a4a48',
  'disabled-text': '#8a9b99',
};

export function paletteFor(scheme: ColourScheme, dark: boolean): Palette {
  const s = SCHEMES[scheme];
  return dark
    ? {
        ...DARK_BASE,
        primary: s.accent,
        'primary-deep': s.deepOnDark,
        accent: s.deepOnDark,
        tile: s.tintOnDark,
        'on-primary': '#ffffff',
      }
    : {
        ...LIGHT_BASE,
        ground: s.ground,
        primary: s.accent,
        'primary-deep': s.deep,
        accent: s.accent,
        tile: s.tint,
        'on-primary': '#ffffff',
      };
}

export const TEXT_SCALE: Record<TextSize, number> = { default: 1, large: 1.15, largest: 1.3 };

export const SCALED_TEXT_PX = { caption: 13, small: 15, body: 16, lead: 17, heading: 28 } as const;

export type ScaledText = keyof typeof SCALED_TEXT_PX;

export const scaledPx = (name: ScaledText, size: TextSize) =>
  Math.round(SCALED_TEXT_PX[name] * TEXT_SCALE[size] * 10) / 10;
