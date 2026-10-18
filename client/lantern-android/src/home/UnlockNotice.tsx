import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { OutlineButton } from '../shared/ui/OutlineButton';

export function UnlockNotice({ onSignOut }: { onSignOut: () => void }) {
  const strings = useStrings();

  return (
    <View className="gap-3 rounded-card border border-line bg-card p-4">
      <Text accessibilityRole="header" className="font-inter-bold text-lead text-ink">
        {strings.home.locked.heading}
      </Text>
      <Text className="font-inter-medium text-small text-muted">{strings.home.locked.text}</Text>
      <View className="self-start">
        <OutlineButton label={strings.common.signOut} onPress={onSignOut} />
      </View>
    </View>
  );
}
