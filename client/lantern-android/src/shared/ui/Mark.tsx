import { View } from 'react-native';
import Svg from 'react-native-svg';

import { useStrings } from '../i18n';
import { MARK_GRID, markLayers } from './markParts';

const SIZE = 120;

export function Mark() {
  const label = useStrings().mark.label;

  return (
    <View accessible accessibilityRole="image" accessibilityLabel={label}>
      <Svg width={SIZE} height={SIZE} viewBox={`0 0 ${MARK_GRID} ${MARK_GRID}`}>
        {markLayers.map(({ part, Draw }) => (
          <Draw key={part} />
        ))}
      </Svg>
    </View>
  );
}
