import { Text } from 'react-native';

export function FailureText({ message }: { message: string | null }) {
  if (message === null) return null;

  return (
    <Text accessibilityLiveRegion="polite" className="type-error">
      {message}
    </Text>
  );
}
