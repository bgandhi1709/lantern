import type { ReactNode } from 'react';
import { Pressable, View } from 'react-native';
import Svg, { Path } from 'react-native-svg';

import { colors } from './theme';

type Props = {
  checked: boolean;
  onToggle: () => void;
  accessibilityLabel: string;
  icon: ReactNode;
  children: ReactNode;
};

// A tick, not just a colour, shows the box is checked.
function Box({ checked }: { checked: boolean }) {
  return (
    <View
      className={`size-7 items-center justify-center rounded-tile border-2 border-primary ${checked ? 'bg-primary' : 'bg-card'}`}
    >
      {checked ? (
        <Svg width={18} height={18} viewBox="0 0 24 24" fill="none">
          <Path
            d="M5 12.5l4.5 4.5L19 7.5"
            stroke={colors.onPrimary}
            strokeWidth={3}
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </Svg>
      ) : null}
    </View>
  );
}

export function CheckRow({ checked, onToggle, accessibilityLabel, icon, children }: Props) {
  return (
    <View className="flex-row items-center gap-3 rounded-card border border-line bg-card py-1 pl-4 pr-1">
      {icon}
      <View className="flex-1 py-2">{children}</View>
      <Pressable
        accessibilityRole="checkbox"
        accessibilityLabel={accessibilityLabel}
        accessibilityState={{ checked }}
        onPress={onToggle}
        className="size-12 items-center justify-center"
      >
        <Box checked={checked} />
      </Pressable>
    </View>
  );
}
