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
      className="min-h-16 flex-row items-center gap-3 surface px-4 py-2.5"
    >
      <View importantForAccessibility="no-hide-descendants" className="icon-tile">
        {subjectGlyphs[subject.icon]}
      </View>
      <View className="flex-1">
        <Text className="type-lead">{subject.name}</Text>
        <Text className="type-small-muted">{chapters}</Text>
      </View>
      <ChevronRightIcon />
    </Pressable>
  );
}
