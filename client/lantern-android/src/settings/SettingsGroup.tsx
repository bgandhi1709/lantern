import type { ReactNode } from 'react';
import { Pressable, Text, View } from 'react-native';

import { ChevronDownIcon } from '../shared/ui/icons';

type Props = { title: string; open: boolean; onToggle: () => void; children: ReactNode };

export function SettingsGroup({ title, open, onToggle, children }: Props) {
  return (
    <View className="gap-2">
      <Pressable
        accessibilityRole="button"
        accessibilityState={{ expanded: open }}
        accessibilityLabel={title}
        onPress={onToggle}
        className="min-h-12 flex-row items-center justify-between px-1"
      >
        <Text className="font-inter-bold text-small uppercase text-muted">{title}</Text>
        <View className={open ? 'rotate-180' : ''}>
          <ChevronDownIcon />
        </View>
      </Pressable>
      {open ? <View className="gap-2">{children}</View> : null}
    </View>
  );
}
