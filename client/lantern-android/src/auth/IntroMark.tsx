import { View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { MARK_GRID, markLayers } from '../shared/ui/markParts';
import { IntroLayer } from './IntroLayer';
import type { IntroValues } from './useIntro';

// 200 px, the same as the size-50 class below.
const SIZE = 200;

export function IntroMark({ values }: { values: IntroValues }) {
  const label = useStrings().mark.label;
  const unit = SIZE / MARK_GRID;

  return (
    <View accessible accessibilityRole="image" accessibilityLabel={label} className="size-50">
      {markLayers.map(({ part, Draw }) => (
        <IntroLayer key={part} value={values[part]} part={part} unit={unit}>
          <Draw />
        </IntroLayer>
      ))}
    </View>
  );
}
