import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { StepHeader } from './StepHeader';

// Stands in for step 2 until the Board screen (#119) replaces it.
export function BoardNextView({ onBack }: { onBack: () => void }) {
  const strings = useStrings().registration;

  return (
    <View className="flex-1 bg-ground px-6 pb-safe pt-safe">
      <StepHeader step={2} total={4} onBack={onBack} />
      <Text accessibilityRole="header" className="mt-5 font-inter-bold text-heading text-ink">
        {strings.boardNext}
      </Text>
    </View>
  );
}
