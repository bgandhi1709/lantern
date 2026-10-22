import { Pressable, Text, View } from 'react-native';

type Option<T extends string> = {
  value: T;
  label: string;
  accessibilityLabel: string;
  textClassName?: string;
};

type Props<T extends string> = {
  label: string;
  options: readonly Option<T>[];
  value: T;
  onPick: (value: T) => void;
};

export function OptionGroup<T extends string>({ label, options, value, onPick }: Props<T>) {
  return (
    <View
      accessibilityRole="radiogroup"
      accessibilityLabel={label}
      className="flex-row gap-2 rounded-card bg-track p-1"
    >
      {options.map((option) => {
        const checked = option.value === value;
        return (
          <Pressable
            key={option.value}
            accessibilityRole="radio"
            accessibilityState={{ checked }}
            accessibilityLabel={option.accessibilityLabel}
            onPress={() => onPick(option.value)}
            className={`min-h-12 flex-1 items-center justify-center rounded-control px-1 ${checked ? 'bg-primary' : ''}`}
          >
            <Text
              className={`font-inter-bold ${option.textClassName ?? 'text-small'} ${checked ? 'text-on-primary' : 'text-ink'}`}
            >
              {option.label}
            </Text>
          </Pressable>
        );
      })}
    </View>
  );
}
