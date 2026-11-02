import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';

// The code can be selected and copied by hand, as well as shared with the two buttons below it.
export function RecoveryCodeCard({ code }: { code: string }) {
  const strings = useStrings().registration.recovery;

  return (
    <View className="items-center gap-2 lantern-card px-4 py-6">
      <Text className="lantern-section-label tracking-wider">{strings.codeLabel}</Text>
      <Text selectable testID="recovery-code" className="lantern-item-title tracking-widest">
        {code}
      </Text>
    </View>
  );
}
