import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { signOutOfLantern } from './googleSignIn';
import { type MeStatus, useMeStatus } from './useMeStatus';

// Stands in for Home until the registration and Home stories replace it.
export function SignedInPlaceholder() {
  const { status, retry } = useMeStatus();
  const strings = useStrings();
  const message: Record<MeStatus, string> = strings.signedIn;

  return (
    <View className="flex-1 items-center justify-center gap-4 bg-ground p-6">
      <Text accessibilityRole="header" className="font-inter-bold text-[36px] text-primary">
        {strings.appName}
      </Text>
      <Text
        accessibilityLiveRegion="polite"
        className="text-center font-inter-medium text-[16px] text-muted"
      >
        {message[status]}
      </Text>
      <PrimaryButton label={strings.signedIn.checkAgain} onPress={retry} />
      <PrimaryButton label={strings.signedIn.signOut} onPress={signOutOfLantern} />
    </View>
  );
}
