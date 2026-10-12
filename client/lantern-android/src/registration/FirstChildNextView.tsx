import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { StepHeader } from './StepHeader';
import { REGISTRATION_STEP_COUNT } from './steps';

// Stands in for step 4 until the First child screen (#120) replaces it.
export function FirstChildNextView({ onBack }: { onBack: () => void }) {
  const strings = useStrings().registration;

  return (
    <View className="flex-1 bg-ground px-6 pb-safe pt-safe">
      <StepHeader step={4} total={REGISTRATION_STEP_COUNT} onBack={onBack} />
      <Text accessibilityRole="header" className="mt-5 font-inter-bold text-heading text-ink">
        {strings.firstChildNext}
      </Text>
    </View>
  );
}
