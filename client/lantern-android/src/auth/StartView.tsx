import { ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { Mark } from '../shared/ui/Mark';
import { FeatureList } from './FeatureList';
import { GoogleButton } from './GoogleButton';
import type { SignInStatus } from './useGoogleSignIn';

type Props = { status: SignInStatus; onSignIn: () => void };

export function StartView({ status, onSignIn }: Props) {
  const strings = useStrings();

  return (
    <View className="flex-1 bg-ground pb-safe pt-safe">
      <ScrollView contentContainerClassName="gap-3 p-6 pb-3">
        <Text className="text-center font-inter-bold text-brand text-primary">
          {strings.appName}
        </Text>
        <View className="items-center">
          <Mark />
        </View>
        <Text
          accessibilityRole="header"
          className="text-center font-inter-bold text-heading text-ink"
        >
          {strings.start.heading}
        </Text>
        <Text className="text-center font-inter-medium text-body text-muted">
          {strings.start.subheading}
        </Text>
        <FeatureList />
      </ScrollView>
      <View className="gap-2 px-6 pb-6">
        <Text
          accessibilityLiveRegion="polite"
          className="text-center font-inter-semibold text-small text-danger"
        >
          {status === 'failed' ? strings.start.signInFailed : null}
        </Text>
        <GoogleButton busy={status === 'signingIn'} onPress={onSignIn} />
        <Text className="text-center font-inter-medium text-caption text-muted">
          {strings.start.note}
        </Text>
      </View>
    </View>
  );
}
