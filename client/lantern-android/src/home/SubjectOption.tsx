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
      className={`min-h-16 flex-row items-center gap-3 rounded-card border-2 px-3.5 py-2.5 ${selected ? 'border-primary bg-tile' : 'border-field bg-card'}`}
    >
      <View
        importantForAccessibility="no-hide-descendants"
        className="size-11 items-center justify-center rounded-control bg-tile"
      >
        {subjectGlyphs[subject.icon]}
      </View>
      <View className="flex-1">
        <Text className="font-inter-bold text-lead text-ink">{subject.name}</Text>
        <Text className="font-inter-medium text-small text-muted">{chapters}</Text>
      </View>
      {selected ? (
        <View className="size-7 items-center justify-center rounded-full bg-primary">
          <TickIcon />
        </View>
      ) : null}
    </Pressable>
  );
}
