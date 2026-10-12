import { ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { BOARDS } from './board';
import type { Board } from './board';
import { BoardCard } from './BoardCard';
import { StepHeader } from './StepHeader';
import { REGISTRATION_STEP_COUNT } from './steps';

type Props = {
  board: Board | null;
  onSelect: (board: Board) => void;
  onContinue: () => void;
  onBack: () => void;
};

export function BoardView({ board, onSelect, onContinue, onBack }: Props) {
  const strings = useStrings().registration.board;

  return (
    <View className="flex-1 bg-ground px-6 pb-safe pt-safe">
      <StepHeader step={2} total={REGISTRATION_STEP_COUNT} onBack={onBack} />
      <ScrollView contentContainerClassName="gap-6 pb-3">
        <View className="mt-5 gap-2">
          <Text accessibilityRole="header" className="font-inter-bold text-heading text-ink">
            {strings.heading}
          </Text>
          <Text className="font-inter-medium text-body text-muted">{strings.intro}</Text>
        </View>
        <View
          accessibilityRole="radiogroup"
          accessibilityLabel={strings.groupLabel}
          className="flex-row items-stretch gap-3"
        >
          {BOARDS.map((option) => (
            <BoardCard
              key={option}
              board={option}
              {...strings.cards[option]}
              selected={board === option}
              onSelect={() => onSelect(option)}
            />
          ))}
        </View>
      </ScrollView>
      <View className="pb-6 pt-2">
        <PrimaryButton label={strings.continue} onPress={onContinue} disabled={board === null} />
      </View>
    </View>
  );
}
