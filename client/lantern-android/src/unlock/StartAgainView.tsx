import { ScrollView, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { OutlineButton } from '../shared/ui/OutlineButton';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { WarningCard } from '../shared/ui/WarningCard';
import { ScreenHeading } from '../shared/ui/ScreenHeading';

type Props = { onDelete: () => void; onKeep: () => void };

export function StartAgainView({ onDelete, onKeep }: Props) {
  const strings = useStrings().startAgain;

  return (
    <View className="lantern-screen">
      <ScrollView contentContainerClassName="gap-4 pb-3 pt-6">
        <ScreenHeading heading={strings.heading} intro={strings.intro} />
        <WarningCard heading={strings.warningHeading} text={strings.warning} />
      </ScrollView>
      <View className="lantern-screen-footer">
        <OutlineButton tone="danger" label={strings.delete} onPress={onDelete} />
        <PrimaryButton label={strings.keep} onPress={onKeep} />
      </View>
    </View>
  );
}
