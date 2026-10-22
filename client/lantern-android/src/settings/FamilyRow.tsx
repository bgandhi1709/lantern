import { Pressable, Text, View } from 'react-native';

type Props = { title: string; text: string; onPress: () => void };

export function FamilyRow({ title, text, onPress }: Props) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={title}
      onPress={onPress}
      className="min-h-12 gap-0.5 rounded-card border border-line bg-card p-4"
    >
      <Text className="font-inter-bold text-lead text-ink">{title}</Text>
      <View>
        <Text className="font-inter-medium text-small text-muted">{text}</Text>
      </View>
    </Pressable>
  );
}
