import { Text, View } from 'react-native';

import { PageTurnLoader } from './PageTurnLoader';
import { useReduceMotion } from './useReduceMotion';

type Props = { label: string };

export function LoadingOverlay({ label }: Props) {
  const reduceMotion = useReduceMotion();

  return (
    <View
      accessible
      accessibilityRole="progressbar"
      accessibilityLabel={label}
      accessibilityLiveRegion="polite"
      className="absolute inset-0 items-center justify-center gap-4 bg-ground"
    >
      <PageTurnLoader still={reduceMotion} />
      <Text className="px-6 text-center font-inter-medium text-body text-muted">{label}</Text>
    </View>
  );
}
