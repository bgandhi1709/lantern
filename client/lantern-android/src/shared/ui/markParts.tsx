import { Circle, Ellipse, G, Path } from 'react-native-svg';

import { markColors as c } from './theme';

// The mark's parts on a 120 x 120 grid, back to front. The intro animates them one by one;
// the still mark draws them all at once.
export const LIGHT_PATH = 'M92 86 C88 78 98 72 94 62 C90 52 96 46 90 37';

export const Arch = () => (
  <Path d="M80 62 V30 a10 10 0 0 1 20 0 V62" stroke={c.arch} strokeWidth={2} fill="none" />
);

export const Mother = () => (
  <G>
    <Path
      d="M31 28 C30 36 30 44 27 52 C25 57 24 60 26 63 C31 61 35 56 37 50 C38 45 38 40 37 35 Z"
      fill={c.motherHair}
    />
    <Path d="M38 38 L46 41 L47 47 L39 47 Z" fill={c.face} />
    <Path
      d="M22 92 C20 80 24 62 33 53 C37 49 43 47 48 48.5 C54 50.5 58 56 60 64 L57 92 L22 92 Z"
      fill={c.motherClothes}
    />
    <Path
      d="M31 26 C31 18 37 14 44 14.5 C50 15 53.5 20 53.5 25.5 C53.5 27.5 55 29.5 56.5 32 C55.6 33 54.4 33.4 53.8 34 C54.4 35.4 53.6 36.6 52.6 37.2 C52.4 39.6 50 41.6 46.5 41.8 C42 42 37.5 40.8 35 38.5 C32 35.5 31 31 31 26 Z"
      fill={c.face}
    />
    <Path
      d="M30.5 27 C30 18 37 13.5 45 14 C51 14.5 54.5 19 54 24 C49 21.5 43 21.5 39 24.5 C37 26.5 36 30 35.5 33 C33 31.5 31 29.5 30.5 27 Z"
      fill={c.motherHair}
    />
    <Path d="M47.6 28.6 q1.6 1.1 3.2 0" stroke={c.motherHair} strokeWidth={1.1} fill="none" />
  </G>
);

export const Child = () => (
  <G>
    <Path d="M64 63 L71 64.5 L71 68.5 L64 68 Z" fill={c.face} />
    <Path d="M54 94 C54 80 58 70 66 67.5 C72.5 66 79 70 81.5 78 L83 94 Z" fill={c.childClothes} />
    <Path
      d="M59 55 C59 48.5 63.5 45 68.5 45.3 C73.5 45.6 76.8 49 76.8 53.2 C77.6 54.6 78.8 55.6 79.4 57.2 C78.6 57.9 77.6 58.1 77.2 58.8 C77.3 61.8 74.8 64.6 70.6 64.8 C65.8 65 61.2 62.6 59.6 59 C59.2 57.8 59 56.4 59 55 Z"
      fill={c.face}
    />
    <Path
      d="M58.6 55.5 C57.6 47.5 63 43.6 68.8 44 C74 44.4 77.6 47.6 77 51.6 C73 49.6 68.6 50 65.6 52.4 C64.6 55.4 63.4 58.2 61.4 60.2 C59.6 59 58.8 57.4 58.6 55.5 Z"
      fill={c.childHair}
    />
    <Path d="M72.4 54.6 q1.3 0.9 2.6 0" stroke={c.childHair} strokeWidth={1} fill="none" />
  </G>
);

export const Book = () => (
  <G>
    <Path
      d="M12 88 Q36 80 60 90 Q84 80 108 88 L108 100 Q84 92 60 102 Q36 92 12 100 Z"
      fill={c.page}
      stroke={c.motherClothes}
      strokeWidth={1.2}
    />
    <Path d="M60 90 V102" stroke={c.light} strokeWidth={1.2} />
  </G>
);

export const Light = () => (
  <G>
    <Path d={LIGHT_PATH} stroke={c.glow} strokeWidth={6} strokeLinecap="round" fill="none" />
    <Path d={LIGHT_PATH} stroke={c.light} strokeWidth={1.6} strokeLinecap="round" fill="none" />
  </G>
);

export const Hands = () => (
  <G>
    <Path
      d="M51 55 C55 66 63 75 75 81"
      stroke={c.motherClothes}
      strokeWidth={7}
      strokeLinecap="round"
      fill="none"
    />
    <Path
      d="M74 73 C78 77 82 81 86 84.5"
      stroke={c.childClothes}
      strokeWidth={5}
      strokeLinecap="round"
      fill="none"
    />
    <Ellipse cx={77.5} cy={82} rx={4} ry={3.2} fill={c.face} />
    <Circle cx={87.2} cy={85.4} r={3.2} fill={c.face} />
  </G>
);

export const Star = () => (
  <Path d="M90 17 L92 24 L99 26 L92 28 L90 35 L88 28 L81 26 L88 24 Z" fill={c.light} />
);
