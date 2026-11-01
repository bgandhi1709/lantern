import type { ReactNode } from 'react';
import { Pressable, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { ChevronDownIcon } from '../shared/ui/icons';

type Props = {
  title: string;
  value: string;
  open: boolean;
  onToggle: () => void;
  children: ReactNode;
};

export function SettingRow({ title, value, open, onToggle, children }: Props) {
  const rowLabel = useStrings().settings.rowLabel;

  return (
    <View className="overflow-hidden surface">
      <Pressable
        accessibilityRole="button"
        accessibilityState={{ expanded: open }}
        accessibilityLabel={rowLabel(title, value)}
        onPress={onToggle}
        className="min-h-14 flex-row items-center gap-3 px-3.5 py-2"
      >
        <Text className="flex-1 type-body-bold">{title}</Text>
        <Text className="type-small-muted">{value}</Text>
        <View className={open ? 'rotate-180' : ''}>
          <ChevronDownIcon />
        </View>
      </Pressable>
      {open ? (
        <View className="gap-2.5 border-t border-line px-3.5 pb-4 pt-3">{children}</View>
      ) : null}
    </View>
  );
}
