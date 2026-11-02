import type { ReactNode } from 'react';
import { Text } from 'react-native';

type Props = { children: ReactNode; className?: string };

export function ErrorText({ children, className = '' }: Props) {
  return (
    <Text accessibilityLiveRegion="polite" className={`lantern-field-error ${className}`}>
      {children}
    </Text>
  );
}
