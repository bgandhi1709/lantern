import { View } from 'react-native';
import Svg from 'react-native-svg';

import { useStrings } from '../i18n';
import { Arch, Book, Child, Hands, Light, Mother, Star } from './markParts';

export function Mark({ size = 120 }: { size?: number }) {
  const label = useStrings().mark.label;

  return (
    <View accessible accessibilityRole="image" accessibilityLabel={label}>
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
