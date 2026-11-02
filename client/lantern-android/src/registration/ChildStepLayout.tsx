import type { ReactNode } from 'react';
import { KeyboardAvoidingView, ScrollView, View } from 'react-native';

import { LoadingOverlay } from '../shared/ui/LoadingOverlay';
import { ScreenHeading } from '../shared/ui/ScreenHeading';

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
    <View className="screen">
      <KeyboardAvoidingView behavior="padding" className="flex-1">
        {header}
        <ScrollView contentContainerClassName="gap-4 pb-3" keyboardShouldPersistTaps="handled">
          <ScreenHeading className="mt-5" heading={heading} intro={intro} />
          {children}
        </ScrollView>
        {footer}
      </KeyboardAvoidingView>
      {workingLabel ? <LoadingOverlay label={workingLabel} /> : null}
    </View>
  );
}
