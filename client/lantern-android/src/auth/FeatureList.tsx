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
    <View className="gap-4 rounded-card border border-line bg-card p-4">
      {order.map((feature) => (
        <View key={feature} className="flex-row items-start gap-3">
          <View className="size-11 items-center justify-center rounded-control bg-tile">
            {icons[feature]}
          </View>
          <View className="flex-1">
            <Text className="font-inter-bold text-lead text-ink">{features[feature].title}</Text>
            <Text className="font-inter-medium text-small text-muted">
              {features[feature].text}
            </Text>
          </View>
        </View>
      ))}
    </View>
  );
}
