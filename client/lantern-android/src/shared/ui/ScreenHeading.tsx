import { Text, View } from 'react-native';

type Props = { heading: string; intro: string; className?: string };

export function ScreenHeading({ heading, intro, className = '' }: Props) {
  return (
    <View className={`gap-2 ${className}`}>
      <Text accessibilityRole="header" className="type-heading">
        {heading}
      </Text>
      <Text className="type-body-muted">{intro}</Text>
    </View>
  );
}
