import { Pressable, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import type { Subject } from './homeApi';
import { TickIcon, subjectGlyphs } from './homeIcons';

type Props = { subject: Subject; selected: boolean; onPick: () => void };

export function SubjectOption({ subject, selected, onPick }: Props) {
  const chapters = useStrings().home.chapterCount(subject.chapterCount);

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ selected }}
      accessibilityLabel={`${subject.name}, ${chapters}`}
      onPress={onPick}
      className={`choice-row ${selected ? 'border-primary bg-tile' : 'border-field bg-card'}`}
    >
      <View importantForAccessibility="no-hide-descendants" className="icon-tile">
        {subjectGlyphs[subject.icon]}
      </View>
      <View className="flex-1">
        <Text className="type-lead">{subject.name}</Text>
        <Text className="type-small-muted">{chapters}</Text>
      </View>
      {selected ? (
        <View className="size-7 items-center justify-center rounded-full bg-primary">
          <TickIcon />
        </View>
      ) : null}
    </Pressable>
  );
}
