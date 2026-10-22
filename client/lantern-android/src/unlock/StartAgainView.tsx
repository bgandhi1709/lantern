import { ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { OutlineButton } from '../shared/ui/OutlineButton';
import { PrimaryButton } from '../shared/ui/PrimaryButton';

type Props = { deleting: boolean; failed: boolean; onDelete: () => void; onKeep: () => void };

export function StartAgainView({ deleting, failed, onDelete, onKeep }: Props) {
  const strings = useStrings().unlock.again;

  return (
    <View className="flex-1 bg-ground px-6 pb-safe pt-safe">
      <ScrollView contentContainerClassName="gap-4 pb-3 pt-6">
        <View className="gap-2">
          <Text accessibilityRole="header" className="font-inter-bold text-heading text-ink">
            {strings.heading}
          </Text>
          <Text className="font-inter-medium text-body text-muted">{strings.intro}</Text>
        </View>
        <View className="gap-1 rounded-card border border-line bg-card p-4">
          <Text className="font-inter-bold text-body text-danger">{strings.warningHeading}</Text>
          <Text className="font-inter-medium text-body text-ink">{strings.warning}</Text>
        </View>
        {failed ? (
          <Text
            accessibilityLiveRegion="polite"
            className="font-inter-semibold text-small text-danger"
          >
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
