import { View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { ChoiceRow } from '../shared/ui/ChoiceRow';
import type { Subject } from './homeApi';
import { SelectedMark, subjectGlyphs } from './homeIcons';

type Props = { subject: Subject; selected: boolean; onPick: () => void };

export function SubjectOption({ subject, selected, onPick }: Props) {
  const chapters = useStrings().home.chapterCount(subject.chapterCount);

  return (
    <ChoiceRow
      role="button"
      selected={selected}
      label={`${subject.name}, ${chapters}`}
      onPress={onPick}
      leading={
        <View importantForAccessibility="no-hide-descendants" className="icon-tile">
          {subjectGlyphs[subject.icon]}
        </View>
      }
      title={subject.name}
      subtitle={chapters}
      trailing={<SelectedMark />}
    />
  );
}
