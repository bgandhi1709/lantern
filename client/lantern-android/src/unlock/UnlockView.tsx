import { KeyboardAvoidingView, ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { BackButton } from '../shared/ui/BackButton';
import { OutlineButton } from '../shared/ui/OutlineButton';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { TextField } from '../shared/ui/TextField';

const UNLOCK_FIELD_MAX_LENGTH = 100;

export type UnlockPhase = 'loading' | 'failed' | 'ready';

type Props = {
  phase: UnlockPhase;
  value: string;
  opening: boolean;
  wrong: boolean;
  somethingWentWrong: boolean;
  secondsLeft: number;
  onChange: (text: string) => void;
  onOpen: () => void;
  onLoadAgain: () => void;
  onBack: () => void;
};

export function UnlockView(props: Props) {
  const strings = useStrings().unlock;
  const waiting = props.secondsLeft > 0;
  const ready = props.phase === 'ready';
  const buttonLabel = waiting
    ? strings.waitFor(props.secondsLeft)
    : props.opening
      ? strings.opening
      : strings.open;

  return (
    <View className="flex-1 bg-ground px-6 pb-safe pt-safe">
      <KeyboardAvoidingView behavior="padding" className="flex-1">
        <View className="h-16 justify-center">
          <BackButton onBack={props.onBack} />
        </View>
        <ScrollView contentContainerClassName="gap-4 pb-3" keyboardShouldPersistTaps="handled">
          <View className="gap-2">
            <Text accessibilityRole="header" className="font-inter-bold text-heading text-ink">
              {strings.heading}
            </Text>
            <Text className="font-inter-medium text-body text-muted">{strings.intro}</Text>
          </View>
          <TextField
            secret
            testID="unlock-field"
            label={strings.label}
            hint={strings.hint}
            value={props.value}
            onChangeText={props.onChange}
            maxLength={UNLOCK_FIELD_MAX_LENGTH}
          />
          {props.phase === 'failed' ? (
            <View className="gap-3">
              <Text
                accessibilityLiveRegion="polite"
                className="font-inter-semibold text-small text-danger"
              >
                {strings.loadFailed}
              </Text>
              <OutlineButton label={strings.tryAgain} onPress={props.onLoadAgain} />
            </View>
          ) : null}
          {props.somethingWentWrong ? (
            <Text
              accessibilityLiveRegion="polite"
              className="font-inter-semibold text-small text-danger"
            >
              {strings.failed}
            </Text>
          ) : null}
        </ScrollView>
        <View className="gap-1 pb-4 pt-2">
          {props.wrong ? (
            <Text
              accessibilityLiveRegion="polite"
              className="pb-2 font-inter-semibold text-small text-danger"
            >
              {strings.wrong}
            </Text>
          ) : null}
          <PrimaryButton
            label={props.phase === 'loading' ? strings.loading : buttonLabel}
            onPress={props.onOpen}
            disabled={!ready || props.value.length === 0 || props.opening || waiting}
          />
        </View>
      </KeyboardAvoidingView>
    </View>
  );
}
