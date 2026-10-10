import { ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { GoogleAccountCard } from './GoogleAccountCard';
import { StepHeader } from './StepHeader';

type Props = { parent: { name: string; email: string }; onBack: () => void };

export function AboutYouView({ parent, onBack }: Props) {
  const strings = useStrings().registration;

  return (
    <View className="flex-1 bg-ground px-6 pb-safe pt-safe">
      <StepHeader step={1} total={4} onBack={onBack} />
      <ScrollView contentContainerClassName="gap-4 pb-3">
        <View className="mt-5 gap-2">
          <Text accessibilityRole="header" className="font-inter-bold text-heading text-ink">
            {strings.about.heading}
          </Text>
          <Text className="font-inter-medium text-body text-muted">{strings.about.intro}</Text>
        </View>
        <GoogleAccountCard name={parent.name} email={parent.email} />
      </ScrollView>
    </View>
  );
}
