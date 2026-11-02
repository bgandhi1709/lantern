import { Text } from 'react-native';

import { useStrings } from '../shared/i18n';
import { TEXT_SIZES, THEME_CHOICES } from '../shared/theme';
import type { ColourScheme, TextSize, ThemeChoice } from '../shared/theme';
import { OptionGroup } from '../shared/ui/OptionGroup';
import { PreviewCard } from './PreviewCard';
import { SchemeGrid } from './SchemeGrid';
import { SettingRow } from './SettingRow';
import { SettingsGroup } from './SettingsGroup';
import { APP_IDS } from './settingsSearch';
import type { Outline } from './useSettingsOutline';

const SAMPLE_CLASS: Record<TextSize, string> = {
  default: 'text-aa-default',
  large: 'text-aa-large',
  largest: 'text-aa-largest',
};

export type Look = {
  theme: ThemeChoice;
  textSize: TextSize;
  colourScheme: ColourScheme;
  onTheme: (theme: ThemeChoice) => void;
  onTextSize: (textSize: TextSize) => void;
  onColourScheme: (colourScheme: ColourScheme) => void;
};

type Props = { look: Look; outline: Outline };

export function AppSettings({ look, outline }: Props) {
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
          <Text className="lantern-hint">{text.theme.note}</Text>
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
          <Text className="lantern-hint">{text.textSize.note}</Text>
        </SettingRow>
      ) : null}
      {outline.matches('colour') ? (
        <SettingRow {...row('colour', text.colour.heading, text.colour[look.colourScheme].name)}>
          <SchemeGrid value={look.colourScheme} onPick={look.onColourScheme} />
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
