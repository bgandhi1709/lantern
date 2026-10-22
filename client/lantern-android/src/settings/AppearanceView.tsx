import { ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { COLOUR_SCHEMES, TEXT_SIZES, THEME_CHOICES, paletteFor } from '../shared/theme';
import type { ColourScheme, TextSize, ThemeChoice } from '../shared/theme';
import { BackButton } from '../shared/ui/BackButton';
import { OptionGroup } from '../shared/ui/OptionGroup';
import { PreviewCard } from './PreviewCard';
import { SchemeCard } from './SchemeCard';
import { FamilyRows } from './FamilyRows';

const SAMPLE_CLASS: Record<TextSize, string> = {
  default: 'text-aa-default',
  large: 'text-aa-large',
  largest: 'text-aa-largest',
};

const SCHEME_ROWS = [COLOUR_SCHEMES.slice(0, 2), COLOUR_SCHEMES.slice(2)];

type Props = {
  theme: ThemeChoice;
  textSize: TextSize;
  colourScheme: ColourScheme;
  onTheme: (theme: ThemeChoice) => void;
  onTextSize: (textSize: TextSize) => void;
  onColourScheme: (colourScheme: ColourScheme) => void;
  onBack: () => void;
  onUnlock?: () => void;
  onStartAgain?: () => void;
};

export function AppearanceView(props: Props) {
  const text = useStrings().appearance;

  return (
    <View className="flex-1 bg-ground pb-safe pt-safe">
      <View className="h-16 flex-row items-center gap-1 pl-4 pr-3">
        <BackButton onBack={props.onBack} />
        <Text accessibilityRole="header" className="font-inter-bold text-lead text-ink">
          {text.title}
        </Text>
      </View>
      <ScrollView contentContainerClassName="gap-6 px-4 pb-8 pt-1">
        <FamilyRows onUnlock={props.onUnlock} onStartAgain={props.onStartAgain} />
        <View className="gap-2.5">
          <Text className="font-inter-bold text-lead text-ink">{text.theme.heading}</Text>
          <OptionGroup
            label={text.theme.heading}
            value={props.theme}
            onPick={props.onTheme}
            options={THEME_CHOICES.map((value) => ({
              value,
              label: text.theme[value],
              accessibilityLabel: text.theme[value],
            }))}
          />
          <Text className="font-inter-medium text-small text-muted">{text.theme.note}</Text>
        </View>
        <View className="gap-2.5">
          <Text className="font-inter-bold text-lead text-ink">{text.textSize.heading}</Text>
          <OptionGroup
            label={text.textSize.heading}
            value={props.textSize}
            onPick={props.onTextSize}
            options={TEXT_SIZES.map((value) => ({
              value,
              label: text.textSize.sample,
              accessibilityLabel: text.textSize[value],
              textClassName: SAMPLE_CLASS[value],
            }))}
          />
          <Text className="font-inter-medium text-small text-muted">{text.textSize.note}</Text>
        </View>
        <View
          accessibilityRole="radiogroup"
          accessibilityLabel={text.colour.heading}
          className="gap-2.5"
        >
          <Text className="font-inter-bold text-lead text-ink">{text.colour.heading}</Text>
          {SCHEME_ROWS.map((row) => (
            <View key={row.join()} className="flex-row gap-2.5">
              {row.map((scheme) => (
                <SchemeCard
                  key={scheme}
                  name={text.colour[scheme].name}
                  note={text.colour[scheme].note}
                  swatch={paletteFor(scheme, false).primary}
                  selected={scheme === props.colourScheme}
                  onPick={() => props.onColourScheme(scheme)}
                />
              ))}
            </View>
          ))}
        </View>
        <View className="gap-2">
          <Text className="font-inter-semibold text-small text-muted">{text.preview.label}</Text>
          <PreviewCard />
        </View>
        <Text className="font-inter-medium text-small text-muted">{text.savedNote}</Text>
      </ScrollView>
    </View>
  );
}
