import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { LockIcon } from '../shared/ui/icons';

// The firm part of the Passphrase step, kept calm: why Lantern cannot reset it.
export function KeepSafeNote() {
  const strings = useStrings().registration.passphrase;

  return (
    <View className="flex-row gap-3 rounded-card border border-line bg-card p-4">
      <LockIcon />
      <View className="flex-1 gap-1">
        <Text className="font-inter-bold text-lead text-ink">{strings.noteTitle}</Text>
        <Text className="font-inter-medium text-body text-muted">{strings.note}</Text>
      </View>
    </View>
  );
}
