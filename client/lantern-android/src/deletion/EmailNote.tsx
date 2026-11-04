import { Text, View } from 'react-native';

import { MailIcon } from '../shared/ui/icons';

export function EmailNote({ text }: { text: string }) {
  return (
    <View className="flex-row items-start gap-2.5">
      <MailIcon />
      <Text className="flex-1 lantern-hint">{text}</Text>
    </View>
  );
}
