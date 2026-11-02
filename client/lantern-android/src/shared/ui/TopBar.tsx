import type { ReactNode } from 'react';
import { Text, View } from 'react-native';

import { BackButton } from './BackButton';

type Props = { title: string; onBack: () => void; children?: ReactNode };

export function TopBar({ title, onBack, children }: Props) {
  return (
    <View className="h-16 flex-row items-center gap-1 pl-4 pr-3">
      <BackButton onBack={onBack} />
      <Text accessibilityRole="header" className="flex-1 type-lead">
        {title}
      </Text>
      {children}
    </View>
  );
}
