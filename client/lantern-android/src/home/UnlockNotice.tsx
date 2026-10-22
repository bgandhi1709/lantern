import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { OutlineButton } from '../shared/ui/OutlineButton';
import { PrimaryButton } from '../shared/ui/PrimaryButton';

type Props = { onUnlock: () => void; onSignOut: () => void };

export function UnlockNotice({ onUnlock, onSignOut }: Props) {
  const strings = useStrings();

  return (
    <View className="gap-3 rounded-card border border-line bg-card p-4">
      <Text accessibilityRole="header" className="font-inter-bold text-lead text-ink">
        {strings.home.locked.heading}
      </Text>
      <Text className="font-inter-medium text-small text-muted">{strings.home.locked.text}</Text>
      <PrimaryButton label={strings.home.locked.unlock} onPress={onUnlock} />
      <View className="self-start">
        <OutlineButton label={strings.common.signOut} onPress={onSignOut} />
      </View>
    </View>
  );
}
