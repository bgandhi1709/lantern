import { Pressable, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';

type Props = {
  levels: number[];
  selected: number | null;
  onSelect: (level: number) => void;
  hint?: string;
};

// A tick-free "pressed" look is not enough on its own, so the picked Class is also filled and heavier.
export function ClassChips({ levels, selected, onSelect, hint }: Props) {
  const strings = useStrings().registration.firstChild;

  return (
    <View>
      <View className="flex-row items-center gap-1.5">
        <Text className="font-inter-bold text-body text-ink">{strings.classLabel}</Text>
      </View>
      <View
        accessibilityRole="radiogroup"
        accessibilityLabel={strings.classLabel}
        className="mt-1.5 flex-row flex-wrap gap-2"
      >
        {levels.map((level) => {
          const picked = level === selected;
          return (
            <Pressable
              key={level}
              accessibilityRole="radio"
              accessibilityLabel={strings.classOption(level)}
              accessibilityState={{ checked: picked }}
              onPress={() => onSelect(level)}
              className={`h-12 w-[17.6%] items-center justify-center rounded-control border-2 ${picked ? 'border-primary bg-primary' : 'border-field bg-card'}`}
            >
              <Text
                className={`font-inter-bold text-lead ${picked ? 'text-on-primary' : 'text-ink'}`}
              >
                {level}
              </Text>
            </Pressable>
          );
        })}
      </View>
      {hint ? <Text className="mt-1 font-inter-medium text-small text-muted">{hint}</Text> : null}
    </View>
  );
}
