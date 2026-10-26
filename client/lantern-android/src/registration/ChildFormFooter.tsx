import { Text, View } from 'react-native';

import { PrimaryButton } from '../shared/ui/PrimaryButton';

type Props = {
  label: string;
  disabled: boolean;
  blockedText: string | null;
  failureText: string | null;
  onSubmit: () => void;
};

export function ChildFormFooter({ label, disabled, blockedText, failureText, onSubmit }: Props) {
  return (
    <View className="gap-2 pb-6 pt-2">
      {blockedText ? (
        <Text
          accessibilityLiveRegion="polite"
          className="text-center font-inter-semibold text-small text-warning"
        >
          {blockedText}
        </Text>
      ) : null}
      {failureText ? (
        <Text
          accessibilityLiveRegion="polite"
          className="text-center font-inter-semibold text-small text-danger"
        >
          {failureText}
        </Text>
      ) : null}
      <PrimaryButton label={label} onPress={onSubmit} disabled={disabled} />
    </View>
  );
}
