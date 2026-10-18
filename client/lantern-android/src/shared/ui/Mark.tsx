import { View } from 'react-native';
import Svg from 'react-native-svg';

import { useStrings } from '../i18n';
import { MARK_GRID, markLayers } from './markParts';

const DEFAULT_MARK_SIZE = 120;

export function Mark({ size = DEFAULT_MARK_SIZE }: { size?: number }) {
  const label = useStrings().mark.label;

  return (
    <View accessible accessibilityRole="image" accessibilityLabel={label}>
      <Svg width={size} height={size} viewBox={`0 0 ${MARK_GRID} ${MARK_GRID}`}>
        {markLayers.map(({ part, Draw }) => (
          <Draw key={part} />
        ))}
      </Svg>
    </View>
  );
}
