import { Text } from 'react-native';

export function FailureText({ message }: { message: string | null }) {
  if (message === null) return null;

  return (
    <Text accessibilityLiveRegion="polite" className="font-inter-semibold text-small text-danger">
      {message}
    </Text>
  );
}
