import { View } from 'react-native';
import Svg, { Circle, G, Path, Rect } from 'react-native-svg';

import { colors } from './theme';

export function Mascot({ width = 150 }: { width?: number }) {
  return (
    <View accessible accessibilityRole="image" accessibilityLabel="A smiling lantern mascot">
      <Svg
        width={width}
        height={(width * 150) / 160}
        viewBox="0 0 160 150"
        fill="none"
        stroke={colors.text}
        strokeWidth={5}
        strokeLinecap="round"
        strokeLinejoin="round"
      >
        <Circle cx={80} cy={78} r={66} fill="#fff1cf" stroke="none" />
        <Path d="M26 24l3 7 7 3-7 3-3 7-3-7-7-3 7-3z" fill="#ffb020" stroke="none" />
        <Path
          d="M136 40l2.2 5.3 5.3 2.2-5.3 2.2-2.2 5.3-2.2-5.3-5.3-2.2 5.3-2.2z"
          fill="#ffb020"
          stroke="none"
        />
        <G transform="translate(20 10)">
          <Path d="M44 28C44 6 76 6 76 28" />
          <Path d="M36 28H84L90 44H30Z" fill="#6aa9ff" />
          <Rect x={28} y={44} width={64} height={62} rx={14} fill="#ffcf33" />
          <Path d="M34 106H86L82 122H38Z" fill="#f58220" />
          <Circle cx={49} cy={72} r={4.5} fill={colors.text} stroke="none" />
          <Circle cx={71} cy={72} r={4.5} fill={colors.text} stroke="none" />
          <Path d="M49 85Q60 96 71 85" />
          <Circle cx={41} cy={83} r={4} fill="#ff9fb5" stroke="none" />
          <Circle cx={79} cy={83} r={4} fill="#ff9fb5" stroke="none" />
        </G>
      </Svg>
    </View>
  );
}
