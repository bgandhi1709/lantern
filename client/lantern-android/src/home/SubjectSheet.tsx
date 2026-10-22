import { Modal, Pressable, ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import type { Subject } from './homeApi';
import { SubjectOption } from './SubjectOption';

type Props = {
  visible: boolean;
  subjects: Subject[];
  selectedId: string | null;
  onPick: (subjectId: string) => void;
  onClose: () => void;
};

export function SubjectSheet({ visible, subjects, selectedId, onPick, onClose }: Props) {
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
            {home.chapters.sheetTitle}
          </Text>
          <ScrollView className="max-h-96 grow-0" contentContainerClassName="gap-2">
            {subjects.map((subject) => (
              <SubjectOption
                key={subject.subjectId}
                subject={subject}
                selected={subject.subjectId === selectedId}
                onPick={() => onPick(subject.subjectId)}
              />
            ))}
          </ScrollView>
        </View>
      </View>
    </Modal>
  );
}
