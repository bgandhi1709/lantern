import { Pressable, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { BackIcon } from '../shared/ui/icons';

type Props = { step: number; total: number; onBack: () => void };

// The step is said in words as well as drawn, so it never relies on colour alone.
export function StepHeader({ step, total, onBack }: Props) {
  const strings = useStrings().registration;
  const bars = Array.from({ length: total }, (_, index) => index + 1);

  return (
    <View>
      <View className="flex-row items-center justify-between">
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={strings.back}
          onPress={onBack}
          className="-ml-3 size-12 items-center justify-center"
        >
          <BackIcon />
        </Pressable>
        <Text className="font-inter-semibold text-small text-muted">
          {strings.step(step, total)}
        </Text>
      </View>
      <View importantForAccessibility="no-hide-descendants" className="mt-2 flex-row gap-2">
        {bars.map((bar) => (
          <View
            key={bar}
            className={`h-2 flex-1 rounded-full ${bar <= step ? 'bg-primary' : 'bg-track'}`}
          />
        ))}
      </View>
    </View>
  );
}
