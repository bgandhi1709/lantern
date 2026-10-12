import { KeyboardAvoidingView, ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { CheckRow } from '../shared/ui/CheckRow';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { TextField } from '../shared/ui/TextField';
import { KeepSafeNote } from './KeepSafeNote';
import { PASSPHRASE_MAX_LENGTH } from './passphrase';
import { StepHeader } from './StepHeader';
import { REGISTRATION_STEP_COUNT } from './steps';

type Props = {
  passphrase: string;
  again: string;
  understood: boolean;
  mismatch: boolean;
  canCreate: boolean;
  // Making the keys takes a second or two on purpose, so the button says so.
  creating: boolean;
  failed: boolean;
  onPassphraseChange: (text: string) => void;
  onAgainChange: (text: string) => void;
  onUnderstoodToggle: () => void;
  onCreate: () => void;
  onBack: () => void;
};

export function PassphraseView({
  passphrase,
  again,
  understood,
  mismatch,
  canCreate,
  creating,
  failed,
  onPassphraseChange,
  onAgainChange,
  onUnderstoodToggle,
  onCreate,
  onBack,
}: Props) {
  const strings = useStrings().registration.passphrase;

  return (
    <KeyboardAvoidingView behavior="padding" className="flex-1 bg-ground px-6 pb-safe pt-safe">
      <StepHeader step={3} total={REGISTRATION_STEP_COUNT} onBack={onBack} />
      <ScrollView contentContainerClassName="gap-4 pb-3" keyboardShouldPersistTaps="handled">
        <View className="mt-5 gap-2">
          <Text accessibilityRole="header" className="font-inter-bold text-heading text-ink">
            {strings.heading}
          </Text>
          <Text className="font-inter-medium text-body text-muted">{strings.intro}</Text>
        </View>
        <KeepSafeNote />
        <TextField
          secret
          testID="passphrase-field"
          label={strings.passphraseLabel}
          hint={strings.passphraseHint}
          value={passphrase}
          onChangeText={onPassphraseChange}
          maxLength={PASSPHRASE_MAX_LENGTH}
        />
        <TextField
          secret
          testID="again-field"
          label={strings.againLabel}
          error={mismatch ? strings.mismatch : undefined}
          value={again}
          onChangeText={onAgainChange}
          maxLength={PASSPHRASE_MAX_LENGTH}
        />
        <CheckRow
          checked={understood}
          onToggle={onUnderstoodToggle}
          accessibilityLabel={strings.understood}
        >
          <Text className="font-inter-medium text-body text-ink">{strings.understood}</Text>
        </CheckRow>
        {failed ? (
          <Text
            accessibilityLiveRegion="polite"
            className="font-inter-semibold text-small text-danger"
          >
            {strings.failed}
          </Text>
        ) : null}
      </ScrollView>
      <View className="pb-6 pt-2">
        <PrimaryButton
          label={creating ? strings.creating : strings.create}
          onPress={onCreate}
          disabled={!canCreate || creating}
        />
      </View>
    </KeyboardAvoidingView>
  );
}
