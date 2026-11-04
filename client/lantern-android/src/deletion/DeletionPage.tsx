import type { ReactNode } from 'react';
import { KeyboardAvoidingView, ScrollView, View } from 'react-native';

import { BackButton } from '../shared/ui/BackButton';

type Props = { onBack: () => void; footer: ReactNode; children: ReactNode };

export function DeletionPage({ onBack, footer, children }: Props) {
  return (
    <View className="lantern-screen">
      <KeyboardAvoidingView behavior="padding" className="flex-1">
        <View className="h-16 flex-row items-center">
          <BackButton onBack={onBack} />
        </View>
        <ScrollView contentContainerClassName="gap-4 pb-3 pt-1" keyboardShouldPersistTaps="handled">
          {children}
        </ScrollView>
        <View className="lantern-screen-footer">{footer}</View>
      </KeyboardAvoidingView>
    </View>
  );
}
