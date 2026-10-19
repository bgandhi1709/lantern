import { Text, View } from 'react-native';
import Svg, { Path } from 'react-native-svg';

import { useStrings } from '../shared/i18n';
import { useColors } from '../shared/theme';

const BOOK_SIZE = 24;

export function PreviewCard() {
  const preview = useStrings().appearance.preview;
  const colors = useColors();

  return (
    <View className="gap-3 rounded-card border border-line bg-card p-4">
      <View className="flex-row items-center gap-3">
        <View
          importantForAccessibility="no-hide-descendants"
          className="size-11 items-center justify-center rounded-control bg-tile"
        >
          <Svg width={BOOK_SIZE} height={BOOK_SIZE} viewBox="0 0 24 24" fill="none">
            <Path
              d="M12 6c-2-1.5-5-2-8-2v14c3 0 6 .5 8 2 2-1.5 5-2 8-2V4c-3 0-6 .5-8 2zM12 6v14"
              stroke={colors.primaryDeep}
              strokeWidth={2.5}
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </Svg>
        </View>
        <View className="shrink">
          <Text className="font-inter-bold text-body text-ink">{preview.title}</Text>
          <Text className="font-inter-medium text-small text-muted">{preview.meta}</Text>
        </View>
      </View>
      <Text className="font-inter-medium text-body text-ink">{preview.body}</Text>
      <View className="h-2 rounded-full bg-track">
        <View className="h-2 w-3/5 rounded-full bg-primary" />
      </View>
      <View
        importantForAccessibility="no-hide-descendants"
        className="min-h-13 items-center justify-center rounded-control bg-primary"
      >
        <Text className="font-inter-bold text-body text-on-primary">{preview.next}</Text>
      </View>
    </View>
  );
}
