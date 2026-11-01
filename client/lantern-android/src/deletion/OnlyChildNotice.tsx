import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { OutlineButton } from '../shared/ui/OutlineButton';

export function OnlyChildNotice({ onDeleteFamily }: { onDeleteFamily: () => void }) {
  const notice = useStrings().deleteChild.onlyChild;

  return (
    <View className="gap-3 rounded-card border border-line bg-card p-4">
      <Text accessibilityRole="header" className="font-inter-bold text-lead text-ink">
        {notice.heading}
      </Text>
      <Text className="font-inter-medium text-body text-muted">{notice.text}</Text>
      <OutlineButton tone="danger" label={notice.action} onPress={onDeleteFamily} />
    </View>
  );
}
