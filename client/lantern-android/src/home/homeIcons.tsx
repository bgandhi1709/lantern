import type { ReactNode } from 'react';
import Svg, { Circle, Path, Rect } from 'react-native-svg';

import { useColors } from '../shared/theme';
import type { SubjectIcon } from './homeApi';

type Tone = 'accent' | 'muted' | 'deep' | 'onPrimary';
type GlyphProps = {
  children: ReactNode;
  size: number;
  tone?: Tone;
  color?: string;
  strokeWidth?: number;
};

function Glyph({ children, size, tone = 'accent', color, strokeWidth = 2.25 }: GlyphProps) {
  const colors = useColors();
  const tones: Record<Tone, string> = {
    accent: colors.accent,
    muted: colors.textSecondary,
    deep: colors.primaryDeep,
    onPrimary: colors.onPrimary,
  };
  return (
    <Svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke={color ?? tones[tone]}
      strokeWidth={strokeWidth}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      {children}
    </Svg>
  );
}

const SUBJECT_ICON_SIZE = 26;

export const subjectGlyphs: Record<SubjectIcon, ReactNode> = {
  science: (
    <Glyph size={SUBJECT_ICON_SIZE}>
      <Path d="M9 3h6M10 3v6L5 19a2 2 0 0 0 1.8 3h10.4A2 2 0 0 0 19 19l-5-10V3" />
    </Glyph>
  ),
  maths: (
    <Glyph size={SUBJECT_ICON_SIZE}>
      <Path d="M5 8h6M8 5v6M13 8h6M5 17h6M13 14l6 6M19 14l-6 6" />
    </Glyph>
  ),
  english: (
    <Glyph size={SUBJECT_ICON_SIZE}>
      <Path d="M4 20L12 4l8 16M7.5 14h9" />
    </Glyph>
  ),
  book: (
    <Glyph size={SUBJECT_ICON_SIZE}>
      <Path d="M12 6c-2-1.5-5-2-8-2v14c3 0 6 .5 8 2 2-1.5 5-2 8-2V4c-3 0-6 .5-8 2zM12 6v14" />
    </Glyph>
  ),
  evs: (
    <Glyph size={SUBJECT_ICON_SIZE}>
      <Path d="M5 19c0-8 5-14 15-14 0 10-6 15-14 15" />
      <Path d="M5 19c3-5 6-8 10-10" />
    </Glyph>
  ),
};

export const SettingsIcon = () => (
  <Glyph size={26} strokeWidth={2.25}>
    <Circle cx={12} cy={12} r={3} />
    <Path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z" />
  </Glyph>
);

export const SignOutIcon = () => (
  <Glyph size={26} strokeWidth={2.5}>
    <Path d="M10 4H6a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h4" />
    <Path d="M16 8l4 4-4 4M20 12H9" />
  </Glyph>
);

export const ChevronDownIcon = () => (
  <Glyph size={18} tone="muted" strokeWidth={2.5}>
    <Path d="M6 9l6 6 6-6" />
  </Glyph>
);

export const ChevronRightIcon = () => (
  <Glyph size={20} tone="muted" strokeWidth={2.5}>
    <Path d="M9 5l7 7-7 7" />
  </Glyph>
);

export const PlusIcon = () => (
  <Glyph size={22} tone="deep" strokeWidth={2.75}>
    <Path d="M12 5v14M5 12h14" />
  </Glyph>
);

export const SearchIcon = () => (
  <Glyph size={22} tone="muted" strokeWidth={2.5}>
    <Circle cx={11} cy={11} r={7} />
    <Path d="M20 20l-4-4" />
  </Glyph>
);

export const HistoryIcon = () => (
  <Glyph size={18} tone="deep">
    <Path d="M3 12a9 9 0 1 0 3-6.7" />
    <Path d="M3 4v5h5" />
    <Path d="M12 7v5l3 2" />
  </Glyph>
);

export const TickIcon = () => (
  <Glyph size={18} tone="onPrimary" strokeWidth={3.5}>
    <Path d="M5 12.5l4.5 4.5L19 7.5" />
  </Glyph>
);

const TAB_ICON_SIZE = 24;

export const tabGlyphs = {
  home: (color: string) => (
    <Glyph size={TAB_ICON_SIZE} color={color}>
      <Path d="M3 11l9-8 9 8M5 10v10h14V10M10 20v-6h4v6" />
    </Glyph>
  ),
  teach: (color: string) => (
    <Glyph size={TAB_ICON_SIZE} color={color}>
      <Path d="M12 6c-2-1.5-5-2-8-2v14c3 0 6 .5 8 2 2-1.5 5-2 8-2V4c-3 0-6 .5-8 2zM12 6v14" />
    </Glyph>
  ),
  check: (color: string) => (
    <Glyph size={TAB_ICON_SIZE} color={color}>
      <Rect x="5" y="4" width="14" height="17" rx="3" />
      <Path d="M9 4V3h6v1M9 13l2.2 2.2L15.5 10.5" />
    </Glyph>
  ),
  progress: (color: string) => (
    <Glyph size={TAB_ICON_SIZE} color={color}>
      <Path d="M5 20V11M12 20V5M19 20v-7" />
    </Glyph>
  ),
};
