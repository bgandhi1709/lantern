import { KeyboardAvoidingView, ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { PlusIcon } from '../shared/ui/icons';
import { OutlineButton } from '../shared/ui/OutlineButton';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import type { Board } from './board';
import { MAX_CHILDREN, isChildComplete, isChildTouched } from './child';
import type { ChildDraft } from './child';
import { ChildCard } from './ChildCard';
import type { ChildCardStatus } from './ChildCard';
import { ChildFields } from './ChildFields';
import type { RegisterFailure } from './registrationError';
import { StepHeader } from './StepHeader';
import { REGISTRATION_STEP_COUNT } from './steps';

type Props = {
  board: Board;
  // The first Child, then the extras.
  drafts: ChildDraft[];
  years: string[];
  now: Date;
  // The extra whose card is open, as an index into `drafts`, or null.
  openIndex: number | null;
  canFinish: boolean;
  extraBlocks: boolean;
  finishing: boolean;
  failure: RegisterFailure | null;
  onChange: (index: number, changes: Partial<ChildDraft>) => void;
  onToggle: (index: number) => void;
  onAdd: () => void;
  onRemove: (index: number) => void;
  onFinish: () => void;
  onBack: () => void;
};

export function FirstChildView({
  board,
  drafts,
  years,
  now,
  openIndex,
  canFinish,
  extraBlocks,
  finishing,
  failure,
  onChange,
  onToggle,
  onAdd,
  onRemove,
  onFinish,
  onBack,
}: Props) {
  const strings = useStrings().registration.firstChild;

  const statusOf = (draft: ChildDraft): ChildCardStatus => {
    if (isChildComplete(draft, board, now)) return 'ready';
    return isChildTouched(draft) ? 'needsMore' : 'optional';
  };

  return (
    <KeyboardAvoidingView behavior="padding" className="flex-1 bg-ground px-6 pb-safe pt-safe">
      <StepHeader step={4} total={REGISTRATION_STEP_COUNT} onBack={onBack} />
      <ScrollView contentContainerClassName="gap-4 pb-3" keyboardShouldPersistTaps="handled">
        <View className="mt-5 gap-2">
          <Text accessibilityRole="header" className="font-inter-bold text-heading text-ink">
            {strings.heading}
          </Text>
          <Text className="font-inter-medium text-body text-muted">{strings.intro}</Text>
        </View>
        <ChildFields
          idPrefix="child-1"
          child={drafts[0]}
          board={board}
          years={years}
          onChange={(changes) => onChange(0, changes)}
        />
        <View className="mt-4 gap-3">
          <View className="gap-0.5">
            <Text accessibilityRole="header" className="font-inter-bold text-lead text-ink">
              {strings.more.heading}
            </Text>
            <Text className="font-inter-medium text-small text-muted">{strings.more.intro}</Text>
          </View>
          {drafts.slice(1).map((draft, offset) => {
            const index = offset + 1;
            return (
              <ChildCard
                key={index}
                number={index + 1}
                name={draft.name}
                classLevel={draft.classLevel}
                status={statusOf(draft)}
                open={openIndex === index}
                onToggle={() => onToggle(index)}
                onRemove={() => onRemove(index)}
              >
                <ChildFields
                  idPrefix={`child-${index + 1}`}
                  child={draft}
                  board={board}
                  years={years}
                  classAsList
                  onChange={(changes) => onChange(index, changes)}
                />
              </ChildCard>
            );
          })}
          {drafts.length < MAX_CHILDREN ? (
            <OutlineButton label={strings.more.add} onPress={onAdd} icon={<PlusIcon />} />
          ) : null}
        </View>
      </ScrollView>
      <View className="gap-2 pb-6 pt-2">
        {extraBlocks ? (
          <Text
            accessibilityLiveRegion="polite"
            className="text-center font-inter-semibold text-small text-warning"
          >
            {strings.more.blocked}
          </Text>
        ) : null}
        {failure ? (
          <Text
            accessibilityLiveRegion="polite"
            className="text-center font-inter-semibold text-small text-danger"
          >
            {strings.failures[failure]}
          </Text>
        ) : null}
        <PrimaryButton
          label={finishing ? strings.finishing : failure ? strings.tryAgain : strings.finish}
          onPress={onFinish}
          disabled={!canFinish || finishing}
        />
      </View>
    </KeyboardAvoidingView>
  );
}
