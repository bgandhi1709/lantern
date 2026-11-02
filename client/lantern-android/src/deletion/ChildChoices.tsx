import { View } from 'react-native';

import { ChildAvatar } from '../home';
import type { HomeChild } from '../home';
import { useStrings } from '../shared/i18n';
import { ChoiceRow } from '../shared/ui/ChoiceRow';
import { CheckCircleIcon } from '../shared/ui/icons';

type Props = { options: HomeChild[]; pickedId: string | null; onPick: (childId: string) => void };

export function ChildChoices({ options, pickedId, onPick }: Props) {
  const strings = useStrings();

  return (
    <View
      accessibilityRole="radiogroup"
      accessibilityLabel={strings.deleteChild.choiceLabel}
      className="gap-2"
    >
      {options.map((child) => {
        const classLabel = strings.home.classLabel(child.classLevel);
        return (
          <ChoiceRow
            key={child.id}
            role="radio"
            selected={child.id === pickedId}
            label={`${child.name}, ${classLabel}`}
            onPress={() => onPick(child.id)}
            leading={<ChildAvatar name={child.name} large />}
            title={child.name}
            subtitle={classLabel}
            trailing={<CheckCircleIcon />}
          />
        );
      })}
    </View>
  );
}
