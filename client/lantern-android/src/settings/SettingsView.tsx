import { ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import type { ColourScheme, TextSize, ThemeChoice } from '../shared/theme';
import { BackButton } from '../shared/ui/BackButton';
import { OutlineButton } from '../shared/ui/OutlineButton';
import { AppSettings } from './AppSettings';
import { FamilyManagement } from './FamilyManagement';
import { SearchField } from './SearchField';
import { APP_IDS, FAMILY_IDS } from './settingsSearch';
import type { Outline } from './useSettingsOutline';

type Props = {
  theme: ThemeChoice;
  textSize: TextSize;
  colourScheme: ColourScheme;
  onTheme: (theme: ThemeChoice) => void;
  onTextSize: (textSize: TextSize) => void;
  onColourScheme: (colourScheme: ColourScheme) => void;
  outline: Outline;
  onBack: () => void;
  onUnlock?: () => void;
  onStartAgain?: () => void;
  onDeleteChild?: () => void;
  onDeleteFamily?: () => void;
};

export function SettingsView({ outline, onBack, ...rest }: Props) {
  const strings = useStrings();
  const { theme, textSize, colourScheme, onTheme, onTextSize, onColourScheme, ...family } = rest;
  const offered = {
    unlock: family.onUnlock,
    deleteChild: family.onDeleteChild,
    deleteFamily: family.onDeleteFamily,
    startAgain: family.onStartAgain,
  };
  const present = [...APP_IDS, ...FAMILY_IDS.filter((id) => offered[id])];
  const nothingFound = outline.searching && !present.some(outline.matches);

  return (
    <View className="flex-1 bg-ground pb-safe pt-safe">
      <View className="h-16 flex-row items-center gap-1 pl-4 pr-3">
        <BackButton onBack={onBack} />
        <Text accessibilityRole="header" className="font-inter-bold text-lead text-ink">
          {strings.settings.title}
        </Text>
      </View>
      <View className="px-4 pb-2.5">
        <SearchField
          value={outline.query}
          onChangeText={outline.setQuery}
          onClear={outline.clearQuery}
        />
      </View>
      <ScrollView contentContainerClassName="gap-2 px-4 pb-8" keyboardShouldPersistTaps="handled">
        {nothingFound ? (
          <View accessibilityRole="alert" className="items-center gap-2 px-2 py-6">
            <Text className="text-center font-inter-bold text-lead text-ink">
              {strings.settings.noMatch(outline.query.trim())}
            </Text>
            <Text className="text-center font-inter-medium text-small text-muted">
              {strings.settings.noMatchHint}
            </Text>
            <OutlineButton label={strings.settings.showAll} onPress={outline.clearQuery} />
          </View>
        ) : null}
        <AppSettings
          {...{ theme, textSize, colourScheme, onTheme, onTextSize, onColourScheme, outline }}
        />
        <FamilyManagement outline={outline} {...family} />
        {outline.searching ? null : (
          <Text className="mt-2 font-inter-medium text-small text-muted">
            {strings.appearance.savedNote}
          </Text>
        )}
      </ScrollView>
    </View>
  );
}
