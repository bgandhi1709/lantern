import { Pressable, Text, View } from 'react-native';

type Props = { title: string; text: string; onPress: () => void; danger?: boolean };

export function FamilyRow({ title, text, onPress, danger = false }: Props) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={title}
      onPress={onPress}
      className="min-h-12 gap-0.5 panel"
    >
      <Text className={`font-inter-bold text-lead ${danger ? 'text-danger' : 'text-ink'}`}>
        {title}
      </Text>
      <View>
        <Text className="type-small-muted">{text}</Text>
      </View>
    </Pressable>
  );
}
