import { KeyboardAvoidingView, ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { LoadingOverlay } from '../shared/ui/LoadingOverlay';
import type { Board } from './board';
import type { ChildDraft } from './child';
import { ChildFields } from './ChildFields';
import { FirstChildFooter } from './FirstChildFooter';
import { MoreChildren } from './MoreChildren';
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

  return (
    <View className="flex-1 bg-ground px-6 pb-safe pt-safe">
      <KeyboardAvoidingView behavior="padding" className="flex-1">
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
          <MoreChildren
            board={board}
            drafts={drafts}
            years={years}
            now={now}
            openIndex={openIndex}
            onChange={onChange}
            onToggle={onToggle}
            onAdd={onAdd}
            onRemove={onRemove}
          />
        </ScrollView>
        <FirstChildFooter
          canFinish={canFinish}
          blocked={extraBlocks && openIndex === null}
          finishing={finishing}
          failure={failure}
          onFinish={onFinish}
        />
      </KeyboardAvoidingView>
      {finishing ? <LoadingOverlay label={strings.working} /> : null}
    </View>
  );
}
