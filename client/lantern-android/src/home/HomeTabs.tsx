import { Pressable, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { useColors } from '../shared/theme';
import { tabGlyphs } from './homeIcons';

const TABS = ['home', 'teach', 'check', 'progress'] as const;
const SELECTED_TAB = 'home';

type Props = { onOpenTab: () => void };

export function HomeTabs({ onOpenTab }: Props) {
  const tabs = useStrings().home.tabs;
  const colors = useColors();

  return (
    <View accessibilityLabel={tabs.label} className="border-t border-line bg-card pb-safe">
      <View className="h-17 flex-row px-2 py-1.5">
        {TABS.map((tab) => {
          const selected = tab === SELECTED_TAB;
          return (
            <Pressable
              key={tab}
              accessibilityRole="tab"
              accessibilityState={{ selected }}
              accessibilityLabel={tabs[tab]}
              onPress={selected ? undefined : onOpenTab}
              className="min-h-12 flex-1 items-center justify-center gap-0.5"
            >
              {tabGlyphs[tab](selected ? colors.accent : colors.textSecondary)}
              <Text
                className={`text-caption ${selected ? 'font-inter-bold text-accent' : 'font-inter-medium text-muted'}`}
              >
                {tabs[tab]}
              </Text>
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}
