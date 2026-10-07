import type { ReactNode } from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import type { Strings } from '../shared/i18n';
import { CheckCircleIcon, ChatIcon, ListIcon } from '../shared/ui/icons';
import { colors, fonts } from '../shared/ui/theme';

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
    <View style={styles.card}>
      {order.map((feature) => (
        <View key={feature} style={styles.row}>
          <View style={styles.iconTile}>{icons[feature]}</View>
          <View style={styles.copy}>
            <Text style={styles.title}>{features[feature].title}</Text>
            <Text style={styles.text}>{features[feature].text}</Text>
          </View>
        </View>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    gap: 16,
    padding: 16,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 16,
    backgroundColor: colors.card,
  },
  row: { flexDirection: 'row', alignItems: 'flex-start', gap: 12 },
  iconTile: {
    width: 44,
    height: 44,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 12,
    backgroundColor: colors.iconTile,
  },
  copy: { flex: 1 },
  title: { fontFamily: fonts.bold, fontSize: 17, lineHeight: 22, color: colors.text },
  text: {
    fontFamily: fonts.medium,
    fontSize: 15,
    lineHeight: 21,
    color: colors.textSecondary,
  },
});
