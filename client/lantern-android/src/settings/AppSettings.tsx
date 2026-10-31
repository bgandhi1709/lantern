import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { COLOUR_SCHEMES, TEXT_SIZES, THEME_CHOICES, paletteFor } from '../shared/theme';
import type { ColourScheme, TextSize, ThemeChoice } from '../shared/theme';
import { OptionGroup } from '../shared/ui/OptionGroup';
import { PreviewCard } from './PreviewCard';
import { SchemeCard } from './SchemeCard';
import { SettingRow } from './SettingRow';
import { SettingsGroup } from './SettingsGroup';
import { APP_IDS } from './settingsSearch';
import type { Outline } from './useSettingsOutline';

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
  outline: Outline;
};

export function AppSettings({ outline, ...look }: Props) {
  const strings = useStrings();
  const text = strings.appearance;
  const row = (id: 'theme' | 'textSize' | 'colour' | 'preview', title: string, value: string) => ({
    title,
    value,
    open: outline.isRowOpen(id),
    onToggle: () => outline.toggleRow(id),
  });

  if (!APP_IDS.some(outline.matches)) return null;

  return (
    <SettingsGroup
      title={strings.settings.groups.app}
      open={outline.isGroupOpen('app')}
      onToggle={() => outline.toggleGroup('app')}
    >
      {outline.matches('theme') ? (
        <SettingRow {...row('theme', text.theme.heading, text.theme[look.theme])}>
          <OptionGroup
            label={text.theme.heading}
            value={look.theme}
            onPick={look.onTheme}
            options={THEME_CHOICES.map((value) => ({
              value,
              label: text.theme[value],
              accessibilityLabel: text.theme[value],
            }))}
          />
          <Text className="font-inter-medium text-small text-muted">{text.theme.note}</Text>
        </SettingRow>
      ) : null}
      {outline.matches('textSize') ? (
        <SettingRow {...row('textSize', text.textSize.heading, text.textSize[look.textSize])}>
          <OptionGroup
            label={text.textSize.heading}
            value={look.textSize}
            onPick={look.onTextSize}
            options={TEXT_SIZES.map((value) => ({
              value,
              label: text.textSize.sample,
              accessibilityLabel: text.textSize[value],
              textClassName: SAMPLE_CLASS[value],
            }))}
          />
          <Text className="font-inter-medium text-small text-muted">{text.textSize.note}</Text>
        </SettingRow>
      ) : null}
      {outline.matches('colour') ? (
        <SettingRow {...row('colour', text.colour.heading, text.colour[look.colourScheme].name)}>
          <View
            accessibilityRole="radiogroup"
            accessibilityLabel={text.colour.heading}
            className="gap-2.5"
          >
            {SCHEME_ROWS.map((schemes) => (
              <View key={schemes.join()} className="flex-row gap-2.5">
                {schemes.map((scheme) => (
                  <SchemeCard
                    key={scheme}
                    name={text.colour[scheme].name}
                    note={text.colour[scheme].note}
                    swatch={paletteFor(scheme, false).primary}
                    selected={scheme === look.colourScheme}
                    onPick={() => look.onColourScheme(scheme)}
                  />
                ))}
              </View>
            ))}
          </View>
        </SettingRow>
      ) : null}
      {outline.matches('preview') ? (
        <SettingRow {...row('preview', text.preview.label, strings.settings.previewValue)}>
          <PreviewCard />
        </SettingRow>
      ) : null}
    </SettingsGroup>
  );
}
