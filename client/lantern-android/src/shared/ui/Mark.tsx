import { View } from 'react-native';
import Svg from 'react-native-svg';

import { Arch, Book, Child, Hands, Light, Mother, Star } from './markParts';

export const MARK_LABEL = 'A mother teaching her child from an open book';

export function Mark({ size = 120 }: { size?: number }) {
  return (
    <View accessible accessibilityRole="image" accessibilityLabel={MARK_LABEL}>
      <Svg width={size} height={size} viewBox="0 0 120 120">
        <Arch />
        <Mother />
        <Child />
        <Book />
        <Light />
        <Hands />
        <Star />
      </Svg>
    </View>
  );
}
