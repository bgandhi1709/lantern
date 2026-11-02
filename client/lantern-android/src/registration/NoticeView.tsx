import { ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { BackButton } from '../shared/ui/BackButton';
import { ScreenHeading } from '../shared/ui/ScreenHeading';

type Props = { version: string; onBack: () => void };

export function NoticeView({ version, onBack }: Props) {
  const notice = useStrings().registration.notice;

  return (
    <View className="screen">
      <View className="flex-row">
        <BackButton onBack={onBack} />
      </View>
      <ScrollView contentContainerClassName="gap-5 pb-6">
        <ScreenHeading heading={notice.heading} intro={notice.intro} />
        {notice.sections.map((section) => (
          <View key={section.title} className="gap-2">
            <Text accessibilityRole="header" className="type-lead">
              {section.title}
            </Text>
            {section.text.map((paragraph) => (
              <Text key={paragraph} className="type-body">
                {paragraph}
              </Text>
            ))}
          </View>
        ))}
        <Text className="type-small-muted">{notice.version(version)}</Text>
      </ScrollView>
    </View>
  );
}
