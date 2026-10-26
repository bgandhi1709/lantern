import { Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import type { ChapterState } from './chaptersApi';
import { TickIcon } from './homeIcons';

const STEPS = ['planned', 'taught', 'checked'] as const;
const STEPS_REACHED: Record<ChapterState, number> = {
  'not-started': 0,
  planned: 1,
  taught: 2,
  checked: 3,
};

export function ChapterStateTracker({ state }: { state: ChapterState }) {
  const steps = useStrings().home.chapters.steps;
  const reached = STEPS_REACHED[state];

  return (
    <View importantForAccessibility="no-hide-descendants" className="flex-1 flex-row">
      {STEPS.map((step, index) => {
        const done = reached > index;
        return (
          <View key={step} className="flex-1 items-center gap-1">
            {index < STEPS.length - 1 ? (
              <View
                className={`absolute -right-1/2 left-1/2 top-3 mx-4 h-0.5 ${reached > index + 1 ? 'bg-primary' : 'bg-track'}`}
              />
            ) : null}
            <View
              className={`size-6 items-center justify-center rounded-full border-2 ${done ? 'border-primary bg-primary' : 'border-field bg-card'}`}
            >
              {done ? <TickIcon /> : null}
            </View>
            <Text
              className={`text-caption ${done ? 'font-inter-bold text-ink' : 'font-inter-medium text-muted'}`}
            >
              {steps[step]}
            </Text>
          </View>
        );
      })}
    </View>
  );
}
