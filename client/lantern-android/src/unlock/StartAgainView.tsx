import { ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { OutlineButton } from '../shared/ui/OutlineButton';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { WarningCard } from '../shared/ui/WarningCard';

type Props = { deleting: boolean; failed: boolean; onDelete: () => void; onKeep: () => void };

export function StartAgainView({ deleting, failed, onDelete, onKeep }: Props) {
  const strings = useStrings().startAgain;

  return (
    <View className="flex-1 bg-ground px-6 pb-safe pt-safe">
      <ScrollView contentContainerClassName="gap-4 pb-3 pt-6">
        <View className="gap-2">
          <Text accessibilityRole="header" className="type-heading">
            {strings.heading}
          </Text>
          <Text className="type-body-muted">{strings.intro}</Text>
        </View>
        <WarningCard heading={strings.warningHeading} text={strings.warning} />
        {failed ? (
          <Text accessibilityLiveRegion="polite" className="type-error">
            {strings.failed}
          </Text>
        ) : null}
      </ScrollView>
      <View className="gap-3 pb-6 pt-2">
        <OutlineButton
          tone="danger"
          label={deleting ? strings.deleting : strings.delete}
          onPress={deleting ? () => undefined : onDelete}
        />
        <PrimaryButton label={strings.keep} onPress={onKeep} disabled={deleting} />
      </View>
    </View>
  );
}
