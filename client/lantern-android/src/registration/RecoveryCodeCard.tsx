import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';

// The code can be selected and copied by hand, as well as shared with the two buttons below it.
export function RecoveryCodeCard({ code }: { code: string }) {
  const strings = useStrings().registration.recovery;

  return (
    <View className="items-center gap-2 surface px-4 py-6">
      <Text className="type-small-muted-bold tracking-wider">{strings.codeLabel}</Text>
      <Text selectable testID="recovery-code" className="type-lead tracking-widest">
        {code}
      </Text>
    </View>
  );
}
