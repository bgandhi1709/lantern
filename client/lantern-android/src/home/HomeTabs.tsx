import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { useColors } from '../shared/theme';
import { tabGlyphs } from './homeIcons';

const LATER_TABS = ['teach', 'check', 'progress'] as const;

export function HomeTabs() {
  const tabs = useStrings().home.tabs;
  const colors = useColors();

  return (
    <View
      accessibilityLabel={tabs.label}
      className="h-17 flex-row border-t border-line bg-card px-2 py-1.5"
    >
      <View
        accessible
        accessibilityRole="tab"
        accessibilityState={{ selected: true }}
        accessibilityLabel={tabs.home}
        className="flex-1 items-center justify-center gap-0.5"
      >
        {tabGlyphs.home(colors.accent)}
        <Text className="font-inter-bold text-caption text-accent">{tabs.home}</Text>
      </View>
      {LATER_TABS.map((tab) => (
        <View
          key={tab}
          accessible
          accessibilityRole="tab"
          accessibilityState={{ disabled: true }}
          accessibilityLabel={tabs.later(tabs[tab])}
          className="flex-1 items-center justify-center gap-0.5"
        >
          {tabGlyphs[tab](colors.textSecondary)}
          <Text className="font-inter-medium text-caption text-disabled-text">{tabs[tab]}</Text>
        </View>
      ))}
    </View>
  );
}
