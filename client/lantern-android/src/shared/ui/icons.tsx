import Svg, { Circle, Path, Rect } from 'react-native-svg';

import { useColors } from '../theme';

function LineIcon({
  children,
  strokeWidth = 2.5,
}: {
  children: React.ReactNode;
  strokeWidth?: number;
}) {
  const colors = useColors();
  return (
    <Svg
      width={26}
      height={26}
      viewBox="0 0 24 24"
      fill="none"
      stroke={colors.text}
      strokeWidth={strokeWidth}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      {children}
    </Svg>
  );
}

export const ListIcon = () => (
  <LineIcon>
    <Path d="M9 6h11M9 12h11M9 18h11M4 6h.01M4 12h.01M4 18h.01" />
  </LineIcon>
);

export const ChatIcon = () => (
  <LineIcon>
    <Path d="M21 12a8 8 0 0 1-11.6 7.1L4 20l1-4.6A8 8 0 1 1 21 12z" />
  </LineIcon>
);

export const CheckCircleIcon = () => (
  <LineIcon>
    <Circle cx={12} cy={12} r={9} />
    <Path d="M8 12.5l2.7 2.7L16 9.5" />
  </LineIcon>
);

export const UserIcon = () => (
  <LineIcon>
    <Circle cx={12} cy={8} r={4} />
    <Path d="M4 21c0-4 3.6-7 8-7s8 3 8 7" />
  </LineIcon>
);

export const MailIcon = () => (
  <LineIcon>
    <Rect x={3} y={5} width={18} height={14} rx={3} />
    <Path d="M3.5 7l8.5 6 8.5-6" />
  </LineIcon>
);

export const PinIcon = () => (
  <LineIcon>
    <Path d="M12 21s-7-6.2-7-11a7 7 0 0 1 14 0c0 4.8-7 11-7 11z" />
    <Circle cx={12} cy={10} r={2.5} />
  </LineIcon>
);

export const ShieldCheckIcon = () => (
  <LineIcon>
    <Path d="M12 3l8 3v6c0 5-3.4 8-8 9-4.6-1-8-4-8-9V6z" />
    <Path d="M8.5 12l2.5 2.5 4.5-5" />
  </LineIcon>
);

export const LockIcon = () => (
  <LineIcon>
    <Rect x={5} y={11} width={14} height={10} rx={3} />
    <Path d="M8 11V8a4 4 0 0 1 8 0v3" />
  </LineIcon>
);

export const ShareIcon = () => (
  <LineIcon>
    <Path d="M12 15V4M8 8l4-4 4 4" />
    <Path d="M5 13v5a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2v-5" />
  </LineIcon>
);

export const BackIcon = () => (
  <LineIcon strokeWidth={2.75}>
    <Path d="M15 5l-7 7 7 7" />
  </LineIcon>
);

export const BookIcon = () => (
  <LineIcon>
    <Path d="M12 6c-2-1.5-5-2-8-2v14c3 0 6 .5 8 2 2-1.5 5-2 8-2V4c-3 0-6 .5-8 2z" />
    <Path d="M12 6v14" />
  </LineIcon>
);

export const CalendarIcon = () => (
  <LineIcon>
    <Rect x={3} y={5} width={18} height={16} rx={3} />
    <Path d="M3 10h18M8 3v4M16 3v4" />
  </LineIcon>
);

export const SchoolIcon = () => (
  <LineIcon>
    <Path d="M2 9l10-5 10 5-10 5z" />
    <Path d="M6 11.5V16c0 1.5 3 3 6 3s6-1.5 6-3v-4.5M22 9v6" />
  </LineIcon>
);

export const PlusIcon = () => (
  <LineIcon strokeWidth={2.75}>
    <Path d="M12 5v14M5 12h14" />
  </LineIcon>
);

export const ChevronDownIcon = () => (
  <LineIcon strokeWidth={2.75}>
    <Path d="M6 9l6 6 6-6" />
  </LineIcon>
);

const GOOGLE_G_SIZE = 20;

export function GoogleG() {
  return (
    <Svg width={GOOGLE_G_SIZE} height={GOOGLE_G_SIZE} viewBox="0 0 48 48">
      <Path
        fill="#EA4335"
        d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"
      />
      <Path
        fill="#4285F4"
        d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z"
      />
      <Path
        fill="#FBBC05"
        d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z"
      />
      <Path
        fill="#34A853"
        d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"
      />
    </Svg>
  );
}
