import { Pressable, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import type { Subject } from './homeApi';
import { ChevronRightIcon, subjectGlyphs } from './homeIcons';

type Props = { subject: Subject; onPress: () => void };

export function SubjectRow({ subject, onPress }: Props) {
  const chapters = useStrings().home.chapterCount(subject.chapterCount);

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`${subject.name}, ${chapters}`}
      onPress={onPress}
      className="min-h-16 flex-row items-center gap-3 lantern-card px-4 py-2.5"
    >
      <View importantForAccessibility="no-hide-descendants" className="lantern-icon-badge">
        {subjectGlyphs[subject.icon]}
      </View>
      <View className="flex-1">
        <Text className="lantern-item-title">{subject.name}</Text>
        <Text className="lantern-hint">{chapters}</Text>
      </View>
      <ChevronRightIcon />
    </Pressable>
  );
}
