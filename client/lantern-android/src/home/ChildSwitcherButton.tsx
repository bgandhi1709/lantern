import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { ChildPill } from './ChildPill';

type Props = { name: string; onPress: () => void };

export function ChildSwitcherButton({ name, onPress }: Props) {
  return (
    <View className="flex-row items-center gap-2">
      <Text className="font-inter-semibold text-small text-muted">
        {useStrings().home.learningWith}
      </Text>
      <ChildPill name={name} onPress={onPress} />
    </View>
  );
}
