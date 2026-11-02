import type { ReactNode } from 'react';
import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import type { Strings } from '../shared/i18n';
import { CheckCircleIcon, ChatIcon, ListIcon } from '../shared/ui/icons';

type Feature = keyof Strings['start']['features'];

const order: Feature[] = ['teach', 'answer', 'progress'];

const icons: Record<Feature, ReactNode> = {
  teach: <ListIcon />,
  answer: <ChatIcon />,
  progress: <CheckCircleIcon />,
};

export function FeatureList() {
  const features = useStrings().start.features;

  return (
    <View className="gap-4 lantern-card-padded">
      {order.map((feature) => (
        <View key={feature} className="flex-row items-start gap-3">
          <View className="lantern-icon-badge">{icons[feature]}</View>
          <View className="flex-1">
            <Text className="lantern-item-title">{features[feature].title}</Text>
            <Text className="lantern-hint">{features[feature].text}</Text>
          </View>
        </View>
      ))}
    </View>
  );
}
