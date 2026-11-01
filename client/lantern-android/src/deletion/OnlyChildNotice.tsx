import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { OutlineButton } from '../shared/ui/OutlineButton';

export function OnlyChildNotice({ onDeleteFamily }: { onDeleteFamily: () => void }) {
  const notice = useStrings().deleteChild.onlyChild;

  return (
    <View className="gap-3 panel">
      <Text accessibilityRole="header" className="type-lead">
        {notice.heading}
      </Text>
      <Text className="type-body-muted">{notice.text}</Text>
      <OutlineButton tone="danger" label={notice.action} onPress={onDeleteFamily} />
    </View>
  );
}
