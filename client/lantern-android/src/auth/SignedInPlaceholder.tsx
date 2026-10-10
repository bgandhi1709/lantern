import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { signOutOfLantern } from './googleSignIn';

// Stands in for Home until the Home story replaces it.
export function SignedInPlaceholder() {
  const strings = useStrings();

  return (
    <View className="flex-1 items-center justify-center gap-4 bg-ground p-6">
      <Text accessibilityRole="header" className="font-inter-bold text-[36px] text-primary">
        {strings.appName}
      </Text>
      <Text
        accessibilityLiveRegion="polite"
        className="text-center font-inter-medium text-[16px] text-muted"
      >
        {strings.signedIn.message}
      </Text>
      <PrimaryButton label={strings.common.signOut} onPress={signOutOfLantern} />
    </View>
  );
}
