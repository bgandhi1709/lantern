import { Text, View } from 'react-native';

type Props = { heading: string; intro: string; className?: string };

export function ScreenHeading({ heading, intro, className = '' }: Props) {
  return (
    <View className={`gap-2 ${className}`}>
      <Text accessibilityRole="header" className="lantern-screen-title">
        {heading}
      </Text>
      <Text className="lantern-intro-text">{intro}</Text>
    </View>
  );
}
