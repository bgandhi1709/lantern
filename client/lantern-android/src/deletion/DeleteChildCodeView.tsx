import { KeyboardAvoidingView, Pressable, ScrollView, Text, View } from 'react-native';

import { useStrings } from '../shared/i18n';
import { BackButton } from '../shared/ui/BackButton';
import { OutlineButton } from '../shared/ui/OutlineButton';
import { PrimaryButton } from '../shared/ui/PrimaryButton';
import { TextField } from '../shared/ui/TextField';
import type { DeleteFailure } from './deleteFailure';
import { FailureText } from './FailureText';
import { CODE_LENGTH } from './useOneTimeCode';
import type { Work } from './useOneTimeCode';

type Props = {
  email: string;
  code: string;
  verified: boolean;
  work: Work | null;
  failure: DeleteFailure | null;
  resent: boolean;
  onTypeCode: (text: string) => void;
  onResend: () => void;
  onDelete: () => void;
  onCancel: () => void;
  onBack: () => void;
};

export function DeleteChildCodeView(props: Props) {
  const strings = useStrings().deleteChild;
  const deleting = props.work === 'acting';
  const hint = hintFor(props, strings);

  return (
    <View className="flex-1 bg-ground px-6 pb-safe pt-safe">
      <KeyboardAvoidingView behavior="padding" className="flex-1">
        <View className="h-16 flex-row items-center">
          <BackButton onBack={props.onBack} />
        </View>
        <ScrollView contentContainerClassName="gap-4 pb-3 pt-1" keyboardShouldPersistTaps="handled">
          <View className="gap-2">
            <Text accessibilityRole="header" className="font-inter-bold text-heading text-ink">
              {strings.codeHeading}
            </Text>
            <Text className="font-inter-medium text-body text-muted">
              {strings.codeIntro(props.email)}
            </Text>
          </View>
          <TextField
            numeric
            label={strings.codeLabel}
            hint={hint}
            value={props.code}
            onChangeText={props.onTypeCode}
            maxLength={CODE_LENGTH}
          />
          <FailureText message={props.failure && strings.failures[props.failure]} />
          <Pressable
            accessibilityRole="button"
            onPress={props.onResend}
            disabled={props.work !== null}
            className="min-h-12 justify-center"
          >
            <Text className="font-inter-bold text-body text-primary-deep underline">
              {strings.resend}
            </Text>
          </Pressable>
        </ScrollView>
        <View className="gap-3 pb-6 pt-2">
          <PrimaryButton
            label={deleting ? strings.deleting : strings.delete}
            onPress={props.onDelete}
            disabled={!props.verified || deleting}
          />
          <OutlineButton label={strings.cancel} onPress={props.onCancel} />
        </View>
      </KeyboardAvoidingView>
    </View>
  );
}

function hintFor(props: Props, strings: ReturnType<typeof useStrings>['deleteChild']) {
  if (props.verified) return strings.verified;
  if (props.work === 'verifying') return strings.checking;
  if (props.work === 'requesting') return strings.resending;
  if (props.resent) return strings.resent;
  return strings.codeHint;
}
