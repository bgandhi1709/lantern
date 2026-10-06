import type { ReactNode } from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { CheckCircleIcon, ChatIcon, ListIcon } from '../shared/ui/icons';
import { colors, fonts } from '../shared/ui/theme';

const features: { icon: ReactNode; title: string; text: string }[] = [
  {
    icon: <ListIcon />,
    title: 'Plan',
    text: 'A step-by-step way to teach one chapter at home.',
  },
  {
    icon: <ChatIcon />,
    title: 'Ask',
    text: "Answers to your child's questions, taken from their own book.",
  },
  {
    icon: <CheckCircleIcon />,
    title: 'Check',
    text: 'See what stuck, so you know where to focus next.',
  },
];

export function FeatureList() {
  return (
    <View style={styles.card}>
      {features.map(({ icon, title, text }) => (
        <View key={title} style={styles.row}>
          <View style={styles.iconTile}>{icon}</View>
          <View style={styles.copy}>
            <Text style={styles.title}>{title}</Text>
            <Text style={styles.text}>{text}</Text>
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
