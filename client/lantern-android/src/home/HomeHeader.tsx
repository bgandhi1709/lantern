import { Pressable, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { Mark } from '../shared/ui/Mark';
import { SignOutIcon } from './homeIcons';

const WORDMARK_MARK_SIZE = 32;

type Props = { onSignOut: () => void };

export function HomeHeader({ onSignOut }: Props) {
  const strings = useStrings();

  return (
    <View className="h-16 flex-row items-center justify-between pl-4 pr-2">
      <View accessible accessibilityLabel={strings.appName} className="flex-row items-center gap-2">
        <View importantForAccessibility="no-hide-descendants">
          <Mark size={WORDMARK_MARK_SIZE} />
        </View>
        <Text className="font-inter-bold text-brand text-ink">{strings.appName}</Text>
      </View>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={strings.common.signOut}
        onPress={onSignOut}
        className="size-12 items-center justify-center"
      >
        <SignOutIcon />
      </Pressable>
    </View>
  );
}
