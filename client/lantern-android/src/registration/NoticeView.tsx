import { ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { BackButton } from '../shared/ui/BackButton';

type Props = { version: string; onBack: () => void };

export function NoticeView({ version, onBack }: Props) {
  const notice = useStrings().registration.notice;

  return (
    <View className="flex-1 bg-ground px-6 pb-safe pt-safe">
      <View className="flex-row">
        <BackButton onBack={onBack} />
      </View>
      <ScrollView contentContainerClassName="gap-5 pb-6">
        <View className="gap-2">
          <Text accessibilityRole="header" className="font-inter-bold text-heading text-ink">
            {notice.heading}
          </Text>
          <Text className="font-inter-medium text-body text-muted">{notice.intro}</Text>
        </View>
        {notice.sections.map((section) => (
          <View key={section.title} className="gap-2">
            <Text accessibilityRole="header" className="font-inter-bold text-lead text-ink">
              {section.title}
            </Text>
            {section.text.map((paragraph) => (
              <Text key={paragraph} className="font-inter-medium text-body text-ink">
                {paragraph}
              </Text>
            ))}
          </View>
        ))}
        <Text className="font-inter-medium text-small text-muted">{notice.version(version)}</Text>
      </ScrollView>
    </View>
  );
}
