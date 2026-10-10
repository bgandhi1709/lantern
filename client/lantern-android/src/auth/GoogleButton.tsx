import { Pressable, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { GoogleG } from '../shared/ui/icons';

type Props = { busy: boolean; onPress: () => void };

export function GoogleButton({ busy, onPress }: Props) {
  const strings = useStrings().start;

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ disabled: busy, busy }}
      disabled={busy}
      onPress={onPress}
      className={`h-14 flex-row items-center justify-center gap-3 rounded-control bg-primary active:opacity-70 ${busy ? 'opacity-70' : ''}`}
    >
      <View className="size-7.5 items-center justify-center rounded-tile bg-card">
        <GoogleG />
      </View>
      <Text className="font-inter-bold text-body text-on-primary">
        {busy ? strings.signingIn : strings.continueWithGoogle}
      </Text>
    </Pressable>
  );
}
