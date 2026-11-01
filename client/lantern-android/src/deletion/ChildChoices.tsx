import { Pressable, Text, View } from 'react-native';

import { ChildAvatar } from '../home';
import type { HomeChild } from '../home';
import { useStrings } from '../shared/i18n';
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
        const checked = child.id === pickedId;
        const classLabel = strings.home.classLabel(child.classLevel);
        return (
          <Pressable
            key={child.id}
            accessibilityRole="radio"
            accessibilityState={{ checked }}
            accessibilityLabel={`${child.name}, ${classLabel}`}
            onPress={() => onPick(child.id)}
            className={`min-h-16 flex-row items-center gap-3 rounded-card border-2 px-3.5 py-2.5 ${checked ? 'border-primary bg-tile' : 'border-field bg-card'}`}
          >
            <ChildAvatar name={child.name} large />
            <View className="flex-1">
              <Text className="type-lead">{child.name}</Text>
              <Text className="type-small-muted">{classLabel}</Text>
            </View>
            {checked ? <CheckCircleIcon /> : null}
          </Pressable>
        );
      })}
    </View>
  );
}
