import type { ReactNode } from 'react';
import { Pressable, Text, View } from 'react-native';
import Svg, { Path } from 'react-native-svg';

import { BookIcon, SchoolIcon } from '../shared/ui/icons';
import { colors } from '../shared/ui/theme';
import type { Board } from './board';

type Props = {
  board: Board;
  title: string;
  subtitle: string;
  classes: string;
  selected: boolean;
  onSelect: () => void;
};

const PICTURES: Record<Board, ReactNode> = { cbse: <BookIcon />, ssc: <SchoolIcon /> };

// A tick, not just a heavier border, shows which card is picked.
function Tick() {
  return (
    <View
      testID="board-tick"
      className="absolute right-2 top-2 size-7 items-center justify-center rounded-full bg-primary"
    >
      <Svg width={18} height={18} viewBox="0 0 24 24" fill="none">
        <Path
          d="M5 12.5l4.5 4.5L19 7.5"
          stroke={colors.onPrimary}
          strokeWidth={3.5}
          strokeLinecap="round"
          strokeLinejoin="round"
        />
      </Svg>
    </View>
  );
}

export function BoardCard({ board, title, subtitle, classes, selected, onSelect }: Props) {
  return (
    <Pressable
      accessibilityRole="radio"
      accessibilityState={{ checked: selected }}
      onPress={onSelect}
      className={`flex-1 items-center gap-3 rounded-card border-2 px-3 pb-4 pt-5 ${selected ? 'border-primary bg-tile' : 'border-field bg-card'}`}
    >
      <View className="size-22 items-center justify-center rounded-full bg-tile">
        {PICTURES[board]}
      </View>
      <View className="flex-1 items-center gap-0.5">
        <Text className="text-center font-inter-bold text-lead text-ink">{title}</Text>
        <Text className="text-center font-inter-medium text-small text-muted">{subtitle}</Text>
      </View>
      <Text className="text-center font-inter-semibold text-small text-muted">{classes}</Text>
      {selected ? <Tick /> : null}
    </Pressable>
  );
}
