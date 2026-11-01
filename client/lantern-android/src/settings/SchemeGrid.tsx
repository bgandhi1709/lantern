import { View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { COLOUR_SCHEMES, paletteFor } from '../shared/theme';
import type { ColourScheme } from '../shared/theme';
import { SchemeCard } from './SchemeCard';

const SCHEME_ROWS = [COLOUR_SCHEMES.slice(0, 2), COLOUR_SCHEMES.slice(2)];

type Props = { value: ColourScheme; onPick: (scheme: ColourScheme) => void };

export function SchemeGrid({ value, onPick }: Props) {
  const text = useStrings().appearance.colour;

  return (
    <View accessibilityRole="radiogroup" accessibilityLabel={text.heading} className="gap-2.5">
      {SCHEME_ROWS.map((schemes) => (
        <View key={schemes.join()} className="flex-row gap-2.5">
          {schemes.map((scheme) => (
            <SchemeCard
              key={scheme}
              name={text[scheme].name}
              note={text[scheme].note}
              swatch={paletteFor(scheme, false).primary}
              selected={scheme === value}
              onPick={() => onPick(scheme)}
            />
          ))}
        </View>
      ))}
    </View>
  );
}
