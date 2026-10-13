import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import type { RegisterFailure } from './registrationError';

type Props = {
  canFinish: boolean;
  // Said only while no extra card is open, so it never speaks while the Parent is still typing.
  blocked: boolean;
  finishing: boolean;
  failure: RegisterFailure | null;
  onFinish: () => void;
};

export function FirstChildFooter({ canFinish, blocked, finishing, failure, onFinish }: Props) {
  const strings = useStrings().registration.firstChild;

  return (
    <View className="gap-2 pb-6 pt-2">
      {blocked ? (
        <Text
          accessibilityLiveRegion="polite"
          className="text-center font-inter-semibold text-small text-warning"
        >
          {strings.more.blocked}
        </Text>
      ) : null}
      {failure ? (
        <Text
          accessibilityLiveRegion="polite"
          className="text-center font-inter-semibold text-small text-danger"
        >
          {strings.failures[failure]}
        </Text>
      ) : null}
      <PrimaryButton
        label={finishing ? strings.finishing : failure ? strings.tryAgain : strings.finish}
        onPress={onFinish}
        disabled={!canFinish || finishing}
      />
    </View>
  );
}
