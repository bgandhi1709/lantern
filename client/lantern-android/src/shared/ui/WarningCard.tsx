import { Text, View } from 'react-native';

type Props = { heading: string; text: string };

export function WarningCard({ heading, text }: Props) {
  return (
    <View className="gap-1 rounded-card border border-line bg-card p-4">
      <Text className="font-inter-bold text-body text-danger">{heading}</Text>
      <Text className="type-body">{text}</Text>
    </View>
  );
}
