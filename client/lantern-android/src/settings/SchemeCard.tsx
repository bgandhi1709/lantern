import { Pressable, Text, View } from 'react-native';
import Svg, { Circle, Path } from 'react-native-svg';

import { useColors } from '../shared/theme';

const SWATCH_SIZE = 28;

type Props = { name: string; note: string; swatch: string; selected: boolean; onPick: () => void };

export function SchemeCard({ name, note, swatch, selected, onPick }: Props) {
  const colors = useColors();

  return (
    <Pressable
      accessibilityRole="radio"
      accessibilityState={{ checked: selected }}
      accessibilityLabel={`${name}. ${note}`}
      onPress={onPick}
      className={`min-h-16 flex-1 flex-row items-center gap-2.5 rounded-card border-2 bg-card p-3 ${selected ? 'border-primary' : 'border-line'}`}
    >
      <View importantForAccessibility="no-hide-descendants">
        <Svg width={SWATCH_SIZE} height={SWATCH_SIZE} viewBox="0 0 28 28">
          <Circle cx={14} cy={14} r={14} fill={swatch} />
          {selected ? (
            <Path
              d="M8 14.5l4 4 8-9"
              fill="none"
              stroke={colors.onPrimary}
              strokeWidth={3}
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          ) : null}
        </Svg>
      </View>
      <View className="shrink">
        <Text className="font-inter-bold text-body text-ink">{name}</Text>
        <Text className="font-inter-medium text-caption text-muted">{note}</Text>
      </View>
    </Pressable>
  );
}
