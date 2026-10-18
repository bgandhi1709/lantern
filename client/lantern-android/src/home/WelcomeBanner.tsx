import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { OutlineButton } from '../shared/ui/OutlineButton';

type Props = { childName: string; onDismiss: () => void };

export function WelcomeBanner({ childName, onDismiss }: Props) {
  const banner = useStrings().home.banner;

  return (
    <View className="gap-2 rounded-card border border-line bg-tile p-4">
      <Text accessibilityRole="header" className="font-inter-bold text-lead text-primary-deep">
        {banner.title}
      </Text>
      <Text className="font-inter-medium text-small text-ink">{banner.text(childName)}</Text>
      <View className="self-start">
        <OutlineButton label={banner.dismiss} onPress={onDismiss} />
      </View>
    </View>
  );
}
