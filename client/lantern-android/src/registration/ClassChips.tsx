import { Pressable, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';

type Props = {
  levels: number[];
  selected: number | null;
  onSelect: (level: number) => void;
  hint?: string;
};

// The picked Class is a filled chip with light text, and a radio that is checked for a screen reader.
export function ClassChips({ levels, selected, onSelect, hint }: Props) {
  const strings = useStrings().registration.firstChild;

  return (
    <View>
      <Text className="font-inter-bold text-body text-ink">{strings.classLabel}</Text>
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
