import { View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { MARK_GRID, markLayers } from '../shared/ui/markParts';
import { IntroLayer } from './IntroLayer';
import type { IntroValues } from './useIntro';

export function IntroMark({ values, size }: { values: IntroValues; size: number }) {
  const label = useStrings().mark.label;
  const unit = size / MARK_GRID;

  return (
    <View
      accessible
      accessibilityRole="image"
      accessibilityLabel={label}
      style={{ width: size, height: size }}
    >
      {markLayers.map(({ part, Draw }) => (
        <IntroLayer key={part} value={values[part]} part={part} unit={unit}>
          <Draw />
        </IntroLayer>
      ))}
    </View>
  );
}
