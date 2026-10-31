import { Text } from 'react-native';

import { useStrings } from '../shared/i18n';
import type { DeleteFailure } from './deleteFailure';

export function FailureText({ failure }: { failure: DeleteFailure | null }) {
  const failures = useStrings().deleteChild.failures;
  if (failure === null) return null;

  return (
    <Text accessibilityLiveRegion="polite" className="font-inter-semibold text-small text-danger">
      {failures[failure]}
    </Text>
  );
}
