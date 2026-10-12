import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { StepHeader } from './StepHeader';
import { REGISTRATION_STEP_COUNT } from './steps';

// Stands in for step 3 until the Passphrase screen (#122) replaces it.
export function PassphraseNextView({ onBack }: { onBack: () => void }) {
  const strings = useStrings().registration;

  return (
    <View className="flex-1 bg-ground px-6 pb-safe pt-safe">
      <StepHeader step={3} total={REGISTRATION_STEP_COUNT} onBack={onBack} />
      <Text accessibilityRole="header" className="mt-5 font-inter-bold text-heading text-ink">
        {strings.passphraseNext}
      </Text>
    </View>
  );
}
