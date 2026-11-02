import { Text, View } from 'react-native';

type Props = { heading: string; text: string };

export function WarningCard({ heading, text }: Props) {
  return (
    <View className="gap-1 lantern-card-padded">
      <Text className="font-inter-bold text-body text-danger">{heading}</Text>
      <Text className="lantern-paragraph">{text}</Text>
    </View>
  );
}
