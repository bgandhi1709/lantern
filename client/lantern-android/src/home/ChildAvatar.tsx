import { Text, View } from 'react-native';

type Props = { name: string; large?: boolean };

export function ChildAvatar({ name, large = false }: Props) {
  const size = large ? 'size-11' : 'size-8';
  const text = large ? 'text-lead' : 'text-small';

  return (
    <View
      importantForAccessibility="no-hide-descendants"
      className={`${size} items-center justify-center rounded-full bg-tile`}
    >
      <Text className={`font-inter-bold text-primary-deep ${text}`}>
        {name.slice(0, 1).toUpperCase()}
      </Text>
    </View>
  );
}
