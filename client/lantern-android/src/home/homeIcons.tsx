import type { ReactNode } from 'react';
import Svg, { Path, Rect } from 'react-native-svg';

import { colors } from '../shared/ui/theme';
import type { SubjectIcon } from './homeApi';

type GlyphProps = { children: ReactNode; size: number; color?: string; strokeWidth?: number };

function Glyph({ children, size, color = colors.primary, strokeWidth = 2.25 }: GlyphProps) {
  return (
    <Svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke={color}
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

export const SignOutIcon = () => (
  <Glyph size={26} strokeWidth={2.5}>
    <Path d="M10 4H6a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h4" />
    <Path d="M16 8l4 4-4 4M20 12H9" />
  </Glyph>
);

export const ChevronDownIcon = () => (
  <Glyph size={18} color={colors.textSecondary} strokeWidth={2.5}>
    <Path d="M6 9l6 6 6-6" />
  </Glyph>
);

export const ChevronRightIcon = () => (
  <Glyph size={20} color={colors.textSecondary} strokeWidth={2.5}>
    <Path d="M9 5l7 7-7 7" />
  </Glyph>
);

export const PlusIcon = () => (
  <Glyph size={22} color={colors.primaryDeep} strokeWidth={2.75}>
    <Path d="M12 5v14M5 12h14" />
  </Glyph>
);

export const TickIcon = () => (
  <Glyph size={18} color={colors.onPrimary} strokeWidth={3.5}>
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
