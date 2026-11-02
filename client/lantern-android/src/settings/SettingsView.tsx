import { ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { OutlineButton } from '../shared/ui/OutlineButton';
import { AppSettings } from './AppSettings';
import type { Look } from './AppSettings';
import { FamilyManagement } from './FamilyManagement';
import type { FamilyActions } from './FamilyManagement';
import { SearchField } from './SearchField';
import { APP_IDS, FAMILY_IDS } from './settingsSearch';
import type { Outline } from './useSettingsOutline';
import { TopBar } from '../shared/ui/TopBar';

type Props = { look: Look; family: FamilyActions; outline: Outline; onBack: () => void };

export function SettingsView({ look, family, outline, onBack }: Props) {
  const strings = useStrings();
  const present = [...APP_IDS, ...FAMILY_IDS.filter((id) => family[id])];
  const nothingFound = outline.searching && !present.some(outline.matches);

  return (
    <View className="flex-1 bg-ground pb-safe pt-safe">
      <TopBar title={strings.settings.title} onBack={onBack} />
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
            <Text className="text-center type-lead">
              {strings.settings.noMatch(outline.query.trim())}
            </Text>
            <Text className="text-center type-small-muted">{strings.settings.noMatchHint}</Text>
            <OutlineButton label={strings.settings.clearSearch} onPress={outline.clearQuery} />
          </View>
        ) : null}
        <AppSettings look={look} outline={outline} />
        <FamilyManagement outline={outline} actions={family} />
        {outline.searching ? null : (
          <Text className="mt-2 type-small-muted">{strings.appearance.savedNote}</Text>
        )}
      </ScrollView>
    </View>
  );
}
