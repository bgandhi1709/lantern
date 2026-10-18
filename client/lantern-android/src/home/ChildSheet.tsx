import { Modal, Pressable, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { AddChildRow } from './AddChildRow';
import { ChildRow } from './ChildRow';
import type { SwitcherChild } from './useHome';

type Props = {
  visible: boolean;
  switcherChildren: SwitcherChild[];
  activeId: string;
  onPick: (childId: string) => void;
  onClose: () => void;
  onAddChild?: () => void;
};

export function ChildSheet({
  visible,
  switcherChildren,
  activeId,
  onPick,
  onClose,
  onAddChild,
}: Props) {
  const home = useStrings().home;

  return (
    <Modal transparent animationType="slide" visible={visible} onRequestClose={onClose}>
      <View className="flex-1 justify-end">
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={home.close}
          onPress={onClose}
          className="absolute inset-0 bg-ink/45"
        />
        <View className="gap-3 rounded-t-3xl bg-card px-4 pb-safe pt-3">
          <View className="h-1 w-10 self-center rounded-full bg-track" />
          <Text accessibilityRole="header" className="font-inter-bold text-lead text-ink">
            {home.sheetTitle}
          </Text>
          <View className="gap-2">
            {switcherChildren.map((child) => (
              <ChildRow
                key={child.id}
                child={child}
                selected={child.id === activeId}
                onPick={() => onPick(child.id)}
              />
            ))}
          </View>
          {onAddChild ? <AddChildRow onPress={onAddChild} /> : null}
        </View>
      </View>
    </Modal>
  );
}
