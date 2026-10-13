import type { ReactNode } from 'react';
import { Pressable, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { ChevronDownIcon } from '../shared/ui/icons';

export type ChildCardStatus = 'optional' | 'needsMore' | 'ready';

type Props = {
  // 2 for the second Child, and so on.
  number: number;
  name: string;
  classLevel: number | null;
  status: ChildCardStatus;
  open: boolean;
  onToggle: () => void;
  onRemove: () => void;
  // The fields, shown while the card is open.
  children: ReactNode;
};

// The status is words, and the "needs more" one is also a different colour, never colour alone.
const STATUS_COLOR: Record<ChildCardStatus, string> = {
  optional: 'text-muted',
  needsMore: 'text-warning',
  ready: 'text-success',
};

export function ChildCard({
  number,
  name,
  classLevel,
  status,
  open,
  onToggle,
  onRemove,
  children,
}: Props) {
  const more = useStrings().registration.firstChild.more;
  const statusText = {
    optional: more.statusOptional,
    needsMore: more.statusNeedsMore,
    ready: more.statusReady(classLevel ?? 0),
  }[status];

  return (
    <View className="overflow-hidden rounded-card border border-line bg-card">
      <Pressable
        accessibilityRole="button"
        accessibilityState={{ expanded: open }}
        onPress={onToggle}
        className="min-h-16 flex-row items-center gap-3 px-4 py-2.5"
      >
        <View
          importantForAccessibility="no-hide-descendants"
          className="size-9 items-center justify-center rounded-full bg-tile"
        >
          <Text className="font-inter-bold text-body text-primary-deep">{number}</Text>
        </View>
        <View className="flex-1">
          <Text className="font-inter-bold text-body text-ink">
            {name.trim() === '' ? more.untitled : name.trim()}
          </Text>
          <Text className={`font-inter-medium text-small ${STATUS_COLOR[status]}`}>
            {statusText}
          </Text>
        </View>
        <View className={open ? 'rotate-180' : ''}>
          <ChevronDownIcon />
        </View>
      </Pressable>
      {open ? (
        <View className="gap-3.5 px-4 pb-4">
          {children}
          <Pressable
            accessibilityRole="button"
            onPress={onRemove}
            className="min-h-12 items-center justify-center self-start rounded-control border-2 border-danger bg-card px-4"
          >
            <Text className="font-inter-bold text-small text-danger">{more.remove}</Text>
          </Pressable>
        </View>
      ) : null}
    </View>
  );
}
