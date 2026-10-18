import { Pressable } from 'react-native';

import { useStrings } from '../i18n';
import { BackIcon } from './icons';

export function BackButton({ onBack }: { onBack: () => void }) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={useStrings().registration.back}
      onPress={onBack}
      className="-ml-3 size-12 items-center justify-center"
    >
      <BackIcon />
    </Pressable>
  );
}
