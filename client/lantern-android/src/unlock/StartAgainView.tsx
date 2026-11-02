import { ScrollView, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { OutlineButton } from '../shared/ui/OutlineButton';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { WarningCard } from '../shared/ui/WarningCard';
import { ErrorText } from '../shared/ui/ErrorText';
import { ScreenHeading } from '../shared/ui/ScreenHeading';

type Props = { deleting: boolean; failed: boolean; onDelete: () => void; onKeep: () => void };

export function StartAgainView({ deleting, failed, onDelete, onKeep }: Props) {
  const strings = useStrings().startAgain;

  return (
    <View className="screen">
      <ScrollView contentContainerClassName="gap-4 pb-3 pt-6">
        <ScreenHeading heading={strings.heading} intro={strings.intro} />
        <WarningCard heading={strings.warningHeading} text={strings.warning} />
        {failed ? <ErrorText>{strings.failed}</ErrorText> : null}
      </ScrollView>
      <View className="footer">
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
