import type { ReactNode } from 'react';
import { KeyboardAvoidingView, ScrollView, Text, View } from 'react-native';

import { LoadingOverlay } from '../shared/ui/LoadingOverlay';

type Props = {
  header: ReactNode;
  heading: string;
  intro: string;
  footer: ReactNode;
  workingLabel: string | null;
  children: ReactNode;
};

export function ChildStepLayout({ header, heading, intro, footer, workingLabel, children }: Props) {
  return (
    <View className="flex-1 bg-ground px-6 pb-safe pt-safe">
      <KeyboardAvoidingView behavior="padding" className="flex-1">
        {header}
        <ScrollView contentContainerClassName="gap-4 pb-3" keyboardShouldPersistTaps="handled">
          <View className="mt-5 gap-2">
            <Text accessibilityRole="header" className="font-inter-bold text-heading text-ink">
              {heading}
            </Text>
            <Text className="font-inter-medium text-body text-muted">{intro}</Text>
          </View>
          {children}
        </ScrollView>
        {footer}
      </KeyboardAvoidingView>
      {workingLabel ? <LoadingOverlay label={workingLabel} /> : null}
    </View>
  );
}
