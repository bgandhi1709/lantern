import { Pressable, Text } from 'react-native';

import { useStrings } from '../shared/i18n';
import { ChevronDownIcon } from './homeIcons';

type Props = { name: string; onPress: () => void };

export function SubjectPill({ name, onPress }: Props) {
  const chapters = useStrings().home.chapters;

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={chapters.switchSubject(name)}
      onPress={onPress}
      className="min-h-12 shrink flex-row items-center gap-2 rounded-full border border-line bg-card px-4 py-1"
    >
      <Text numberOfLines={1} className="shrink font-inter-bold text-small text-ink">
        {name}
      </Text>
      <ChevronDownIcon />
    </Pressable>
  );
}
